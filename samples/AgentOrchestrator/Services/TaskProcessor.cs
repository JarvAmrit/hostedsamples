using AgentOrchestrator.Configuration;
using AgentOrchestrator.Models;

namespace AgentOrchestrator.Services;

/// <summary>
/// Processes a task locally. Override or replace this class to plug in your own logic.
/// By default, it echoes the input with a note that this is a passthrough.
/// In a real scenario, you would call your hosted agent's /api/chat endpoint here.
/// </summary>
public class TaskProcessor
{
    private readonly OrchestratorOptions _options;
    private readonly ILogger<TaskProcessor> _logger;

    public TaskProcessor(OrchestratorOptions options, ILogger<TaskProcessor> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Process the task locally and return a step result.
    /// Override this method to implement custom agent logic.
    /// </summary>
    public virtual Task<AgentStepResult> ProcessAsync(TaskPayload task, CancellationToken ct = default)
    {
        var safeTaskId = task.TaskId.Replace("\n", "").Replace("\r", "");
        _logger.LogInformation("[TaskProcessor] Agent '{AgentId}' processing task {TaskId}",
            _options.AgentId, safeTaskId);

        // Default implementation: echo/passthrough
        // Replace this with your actual agent logic (e.g., call your hosted agent's /api/chat)
        var result = new AgentStepResult
        {
            AgentId = _options.AgentId,
            Output = $"[{_options.AgentName}] Processed: {task.Input}",
            Status = "completed",
            CompletedAt = DateTimeOffset.UtcNow,
        };

        return Task.FromResult(result);
    }
}
