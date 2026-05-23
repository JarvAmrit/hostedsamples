using AgentOrchestrator.Configuration;
using AgentOrchestrator.Models;

namespace AgentOrchestrator.Services;

/// <summary>
/// Core orchestration logic: processes a task locally, then optionally hands off to downstream.
/// </summary>
public sealed class OrchestratorService
{
    private readonly OrchestratorOptions _options;
    private readonly TaskProcessor _processor;
    private readonly AgentConnector _connector;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(
        OrchestratorOptions options,
        TaskProcessor processor,
        AgentConnector connector,
        ILogger<OrchestratorService> logger)
    {
        _options = options;
        _processor = processor;
        _connector = connector;
        _logger = logger;
    }

    /// <summary>
    /// Execute the full orchestration: process locally, then hand off if configured.
    /// </summary>
    public async Task<TaskResult> ExecuteAsync(TaskPayload task, string? callbackUrl = null, CancellationToken ct = default)
    {
        var safeTaskId = SanitizeForLog(task.TaskId);
        _logger.LogInformation("[Orchestrator] Executing task {TaskId} on agent '{AgentId}'",
            safeTaskId, _options.AgentId);

        // Step 1: Process locally
        var stepResult = await _processor.ProcessAsync(task, ct);
        task.History.Add(stepResult);

        // Step 2: If auto-handoff is enabled and there's a downstream agent, hand off
        if (_options.AutoHandoff && _connector.HasDownstream)
        {
            _logger.LogInformation("[Orchestrator] Auto-handing off task {TaskId} to downstream", safeTaskId);

            task.SourceAgentId = _options.AgentId;
            var downstreamResult = await _connector.HandoffAsync(task, callbackUrl, ct);

            return new TaskResult
            {
                TaskId = task.TaskId,
                Status = downstreamResult.Status,
                FinalOutput = downstreamResult.FinalOutput,
                Steps = downstreamResult.Steps,
                HandedOffTo = _options.DownstreamAgentUrl,
            };
        }

        // No handoff – this is the terminal agent
        return new TaskResult
        {
            TaskId = task.TaskId,
            Status = stepResult.Status,
            FinalOutput = stepResult.Output,
            Steps = task.History,
        };
    }

    /// <summary>
    /// Explicitly hand off a task to the downstream agent without local processing.
    /// </summary>
    private static string SanitizeForLog(string value) =>
        value.Replace("\n", "").Replace("\r", "");

    public async Task<TaskResult> HandoffAsync(TaskPayload task, string? callbackUrl = null, CancellationToken ct = default)
    {
        if (!_connector.HasDownstream)
        {
            return new TaskResult
            {
                TaskId = task.TaskId,
                Status = "failed",
                Error = "No downstream agent configured for explicit handoff.",
                Steps = task.History,
            };
        }

        task.SourceAgentId = _options.AgentId;
        return await _connector.HandoffAsync(task, callbackUrl, ct);
    }
}
