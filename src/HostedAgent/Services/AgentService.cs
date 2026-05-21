using Azure.AI.Agents.Persistent;
using Azure.Identity;
using HostedAgent.Configuration;
using HostedAgent.Models;

namespace HostedAgent.Services;

/// <summary>
/// Manages the lifecycle of the Azure AI Persistent Agent:
/// creates the agent on first use (or reuses an existing one),
/// and exposes a <see cref="ChatAsync"/> method for conducting conversations.
/// </summary>
public sealed class AgentService : IAsyncDisposable
{
    private readonly AgentOptions _options;
    private readonly ResolvedTools _resolved;
    private readonly ILogger<AgentService> _logger;
    private readonly PersistentAgentsClient _client;

    private PersistentAgent? _agent;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AgentService(
        AgentOptions options,
        ToolResolver toolResolver,
        ILogger<AgentService> logger)
    {
        _options  = options;
        _resolved = toolResolver.Resolve();
        _logger   = logger;
        _client   = CreateClient();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a summary of the current agent (ID, name, model, tools).
    /// Triggers agent initialisation if not yet done.
    /// </summary>
    public async Task<AgentInfoResponse> GetAgentInfoAsync(CancellationToken ct = default)
    {
        var agent = await EnsureAgentAsync(ct);
        return new AgentInfoResponse
        {
            AgentId      = agent.Id,
            AgentName    = agent.Name,
            Model        = agent.Model,
            SystemPrompt = agent.Instructions ?? string.Empty,
            Tools        = agent.Tools.Select(t => t.GetType().Name).ToList(),
        };
    }

    /// <summary>
    /// Sends <paramref name="request"/> to the agent and waits for a reply.
    /// </summary>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message must not be empty.", nameof(request));

        var agent = await EnsureAgentAsync(ct);

        // Create or reuse thread
        PersistentAgentThread thread;
        if (string.IsNullOrWhiteSpace(request.ThreadId))
        {
            var createResp = await _client.Threads.CreateThreadAsync(cancellationToken: ct);
            thread = createResp.Value;
        }
        else
        {
            var getResp = await _client.Threads.GetThreadAsync(request.ThreadId, ct);
            thread = getResp.Value;
        }

        // Post user message
        await _client.Messages.CreateMessageAsync(
            thread.Id,
            MessageRole.User,
            request.Message,
            cancellationToken: ct);

        // Start the run
        ThreadRun run;
        if (_resolved.RunResources is not null)
        {
            var runResp = await _client.Runs.CreateRunAsync(thread, agent, _resolved.RunResources, ct);
            run = runResp.Value;
        }
        else
        {
            var runResp = await _client.Runs.CreateRunAsync(thread, agent, ct);
            run = runResp.Value;
        }

        // Poll until terminal state, handling MCP tool approvals
        run = await PollToCompletionAsync(run, thread.Id, ct);

        // Collect the latest assistant message
        return await BuildResponseAsync(run, thread.Id, ct);
    }

    // ── Agent initialisation ─────────────────────────────────────────────────

    private async Task<PersistentAgent> EnsureAgentAsync(CancellationToken ct)
    {
        if (_agent is not null)
            return _agent;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_agent is not null)
                return _agent;

            if (_options.ReuseExistingAgent)
            {
                var existing = await FindExistingAgentAsync(ct);
                if (existing is not null)
                {
                    _logger.LogInformation(
                        "Reusing existing agent '{Name}' (ID: {Id}).",
                        existing.Name, existing.Id);
                    _agent = existing;
                    return _agent;
                }
            }

            _logger.LogInformation(
                "Creating agent '{Name}' with model '{Model}' and {ToolCount} tool(s).",
                _options.AgentName, _options.ModelDeploymentName, _resolved.Tools.Count);

            var agentResp = await _client.Administration.CreateAgentAsync(
                model:         _options.ModelDeploymentName,
                name:          _options.AgentName,
                instructions:  _options.SystemPrompt,
                tools:         _resolved.Tools.Count > 0 ? _resolved.Tools : null,
                toolResources: _resolved.AgentResources,
                cancellationToken: ct);

            _agent = agentResp.Value;
            _logger.LogInformation("Agent created with ID: {Id}", _agent.Id);
            return _agent;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<PersistentAgent?> FindExistingAgentAsync(CancellationToken ct)
    {
        await foreach (var a in _client.Administration.GetAgentsAsync(cancellationToken: ct))
        {
            if (string.Equals(a.Name, _options.AgentName, StringComparison.OrdinalIgnoreCase))
                return a;
        }
        return null;
    }

    // ── Run polling ───────────────────────────────────────────────────────────

    private static readonly IReadOnlySet<RunStatus> TerminalStatuses =
        new HashSet<RunStatus>
        {
            RunStatus.Completed,
            RunStatus.Failed,
            RunStatus.Cancelled,
            RunStatus.Expired,
        };

    private async Task<ThreadRun> PollToCompletionAsync(
        ThreadRun run, string threadId, CancellationToken ct)
    {
        while (!TerminalStatuses.Contains(run.Status))
        {
            if (run.Status == RunStatus.RequiresAction
                && run.RequiredAction is SubmitToolApprovalAction approvalAction)
            {
                run = await HandleMcpApprovalsAsync(run, approvalAction, ct);
                continue;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(800), ct);
            var runResp = await _client.Runs.GetRunAsync(threadId, run.Id, ct);
            run = runResp.Value;
        }
        return run;
    }

    private async Task<ThreadRun> HandleMcpApprovalsAsync(
        ThreadRun run,
        SubmitToolApprovalAction approvalAction,
        CancellationToken ct)
    {
        var approvals = new List<ToolApproval>();

        foreach (var toolCall in approvalAction.SubmitToolApproval.ToolCalls)
        {
            if (toolCall is not RequiredMcpToolCall mcpCall)
                continue;

            var toolOpt = FindMcpToolOption(mcpCall.Name);
            var approval = new ToolApproval(mcpCall.Id, approve: true);

            if (toolOpt is not null)
                foreach (var (key, value) in toolOpt.McpHeaders)
                    approval.Headers[key] = value;

            _logger.LogDebug(
                "Auto-approving MCP tool call '{Name}' (id={Id}).",
                mcpCall.Name, mcpCall.Id);

            approvals.Add(approval);
        }

        if (approvals.Count == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(800), ct);
            var runResp = await _client.Runs.GetRunAsync(run.ThreadId, run.Id, ct);
            return runResp.Value;
        }

        var submitResp = await _client.Runs.SubmitToolOutputsToRunAsync(
            run,
            toolOutputs: [],
            toolApprovals: approvals,
            cancellationToken: ct);
        return submitResp.Value;
    }

    private ToolOptions? FindMcpToolOption(string toolName)
    {
        foreach (var opt in _options.Tools)
            if (opt.Type == ToolType.Mcp && opt.McpAllowedTools.Contains(toolName))
                return opt;

        return _options.Tools.FirstOrDefault(o => o.Type == ToolType.Mcp);
    }

    // ── Response building ─────────────────────────────────────────────────────

    private async Task<ChatResponse> BuildResponseAsync(
        ThreadRun run, string threadId, CancellationToken ct)
    {
        if (run.Status != RunStatus.Completed)
        {
            return new ChatResponse
            {
                ThreadId = threadId,
                Status   = run.Status.ToString(),
                Error    = run.LastError?.Message ?? "Run did not complete.",
            };
        }

        var responseText = string.Empty;
        var citations    = new List<CitationInfo>();

        await foreach (var msg in _client.Messages.GetMessagesAsync(
            threadId,
            runId: null,
            limit: null,
            order: ListSortOrder.Ascending,
            after: null,
            before: null,
            cancellationToken: ct))
        {
            if (msg.Role != MessageRole.Agent)
                continue;

            foreach (var content in msg.ContentItems)
            {
                if (content is not MessageTextContent textContent)
                    continue;

                var text = textContent.Text;

                if (textContent.Annotations is { Count: > 0 } annotations)
                {
                    foreach (var annotation in annotations)
                    {
                        if (annotation is MessageTextUriCitationAnnotation uriAnnotation)
                        {
                            citations.Add(new CitationInfo
                            {
                                Title = uriAnnotation.UriCitation.Title,
                                Uri   = uriAnnotation.UriCitation.Uri.ToString(),
                            });
                            text = text.Replace(
                                uriAnnotation.Text,
                                $"[{uriAnnotation.UriCitation.Title}]");
                        }
                    }
                }

                responseText = text;
            }
        }

        return new ChatResponse
        {
            ThreadId  = threadId,
            Response  = responseText,
            Citations = citations,
            Status    = run.Status.ToString(),
        };
    }

    // ── Client factory ────────────────────────────────────────────────────────

    private PersistentAgentsClient CreateClient()
    {
        // PersistentAgentsClient requires a string endpoint and TokenCredential.
        // Use DefaultAzureCredential which supports env vars, managed identity,
        // Azure CLI, service principal (AZURE_CLIENT_ID / AZURE_CLIENT_SECRET / AZURE_TENANT_ID), etc.
        _logger.LogInformation(
            "Connecting to Azure AI Foundry endpoint: {Endpoint}", _options.ProjectEndpoint);
        return new PersistentAgentsClient(_options.ProjectEndpoint, new DefaultAzureCredential());
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _initLock.Dispose();
        await ValueTask.CompletedTask;
    }
}

