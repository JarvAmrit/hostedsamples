using System.Net.Http.Json;
using AgentOrchestrator.Configuration;
using AgentOrchestrator.Models;

namespace AgentOrchestrator.Services;

/// <summary>
/// A2A client — sends JSON-RPC 2.0 requests to downstream agents using the A2A protocol.
/// Handles discovery (AgentCard) and task delegation (message/send).
/// </summary>
public sealed class A2AClient
{
    private readonly OrchestratorOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<A2AClient> _logger;

    public A2AClient(OrchestratorOptions options, HttpClient httpClient, ILogger<A2AClient> logger)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(options.DownstreamTimeoutSeconds);
    }

    /// <summary>Whether a downstream agent URL is configured.</summary>
    public bool HasDownstream => !string.IsNullOrWhiteSpace(_options.DownstreamAgentUrl);

    /// <summary>
    /// Discovers the downstream agent's capabilities by fetching its AgentCard.
    /// </summary>
    public async Task<AgentCard?> DiscoverAsync(CancellationToken ct = default)
    {
        if (!HasDownstream) return null;

        var url = _options.DownstreamAgentUrl!.TrimEnd('/') + "/.well-known/agent.json";
        try
        {
            return await _httpClient.GetFromJsonAsync<AgentCard>(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[A2AClient] Failed to discover agent at {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Sends an A2A message/send request to the downstream agent (task handoff).
    /// </summary>
    public async Task<A2ATask?> SendMessageAsync(A2AMessage message, Dictionary<string, string>? metadata = null, CancellationToken ct = default)
    {
        if (!HasDownstream)
        {
            _logger.LogWarning("[A2AClient] No downstream agent configured.");
            return null;
        }

        var url = _options.DownstreamAgentUrl!.TrimEnd('/') + "/a2a";

        var rpcRequest = new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = "message/send",
            Params = new JsonRpcParams
            {
                Message = message,
                Metadata = metadata,
            },
        };

        _logger.LogInformation("[A2AClient] Sending message/send to {Url}", url);

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(rpcRequest),
            };

            if (!string.IsNullOrWhiteSpace(_options.DownstreamAgentApiKey))
            {
                httpRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.DownstreamAgentApiKey);
            }

            var response = await _httpClient.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var rpcResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>(ct);

            if (rpcResponse?.Error != null)
            {
                _logger.LogError("[A2AClient] RPC error: {Code} {Message}",
                    rpcResponse.Error.Code, rpcResponse.Error.Message);
                return null;
            }

            // Deserialize result as A2ATask
            if (rpcResponse?.Result is System.Text.Json.JsonElement element)
            {
                return System.Text.Json.JsonSerializer.Deserialize<A2ATask>(element.GetRawText());
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[A2AClient] Failed to send message to downstream agent");
            return null;
        }
    }

    /// <summary>
    /// Gets the status/result of a task on the downstream agent.
    /// </summary>
    public async Task<A2ATask?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        if (!HasDownstream) return null;

        var url = _options.DownstreamAgentUrl!.TrimEnd('/') + "/a2a";

        var rpcRequest = new JsonRpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Method = "tasks/get",
            Params = new JsonRpcParams { Id = taskId },
        };

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(rpcRequest),
            };

            if (!string.IsNullOrWhiteSpace(_options.DownstreamAgentApiKey))
            {
                httpRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.DownstreamAgentApiKey);
            }

            var response = await _httpClient.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var rpcResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse>(ct);

            if (rpcResponse?.Result is System.Text.Json.JsonElement element)
            {
                return System.Text.Json.JsonSerializer.Deserialize<A2ATask>(element.GetRawText());
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[A2AClient] Failed to get task {TaskId}", taskId);
            return null;
        }
    }
}
