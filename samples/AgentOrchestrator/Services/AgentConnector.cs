using System.Net.Http.Json;
using AgentOrchestrator.Configuration;
using AgentOrchestrator.Models;

namespace AgentOrchestrator.Services;

/// <summary>
/// Connects to a downstream agent and hands off tasks via HTTP.
/// This is the outbound connector – it sends tasks to the next agent in the chain.
/// </summary>
public sealed class AgentConnector
{
    private readonly OrchestratorOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentConnector> _logger;

    public AgentConnector(OrchestratorOptions options, HttpClient httpClient, ILogger<AgentConnector> logger)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(options.DownstreamTimeoutSeconds);
    }

    private static string SanitizeForLog(string value) =>
        value.Replace("\n", "").Replace("\r", "");

    /// <summary>
    /// Whether a downstream agent is configured.
    /// </summary>
    public bool HasDownstream => !string.IsNullOrWhiteSpace(_options.DownstreamAgentUrl);

    /// <summary>
    /// Hands off a task to the downstream agent.
    /// </summary>
    public async Task<TaskResult> HandoffAsync(TaskPayload task, string? callbackUrl = null, CancellationToken ct = default)
    {
        if (!HasDownstream)
        {
            return new TaskResult
            {
                TaskId = task.TaskId,
                Status = "completed",
                FinalOutput = "No downstream agent configured. Task terminates here.",
                Steps = task.History,
            };
        }

        var url = _options.DownstreamAgentUrl!.TrimEnd('/') + "/api/tasks/receive";
        var safeTaskId = SanitizeForLog(task.TaskId);
        _logger.LogInformation("[AgentConnector] Handing off task {TaskId} to {Url}", safeTaskId, url);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new HandoffRequest
            {
                Task = task,
                CallbackUrl = callbackUrl,
            }),
        };

        if (!string.IsNullOrWhiteSpace(_options.DownstreamAgentApiKey))
        {
            request.Headers.Add("X-Agent-Key", _options.DownstreamAgentApiKey);
        }

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TaskResult>(ct);
            return result ?? new TaskResult
            {
                TaskId = task.TaskId,
                Status = "failed",
                Error = "Downstream agent returned null response.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgentConnector] Failed to hand off task {TaskId}", safeTaskId);
            return new TaskResult
            {
                TaskId = task.TaskId,
                Status = "failed",
                Error = $"Handoff failed: {ex.Message}",
                Steps = task.History,
            };
        }
    }
}
