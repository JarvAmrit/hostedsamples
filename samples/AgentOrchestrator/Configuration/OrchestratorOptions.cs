namespace AgentOrchestrator.Configuration;

/// <summary>
/// Configuration loaded from environment variables for the orchestrator.
/// </summary>
public sealed class OrchestratorOptions
{
    /// <summary>Unique identifier for this agent in the orchestration chain.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Display name for this agent.</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// URL of the downstream agent to hand off tasks to.
    /// If empty, this agent is the terminal agent in the chain.
    /// </summary>
    public string? DownstreamAgentUrl { get; set; }

    /// <summary>
    /// Optional API key for authenticating with the downstream agent.
    /// </summary>
    public string? DownstreamAgentApiKey { get; set; }

    /// <summary>
    /// Optional API key that upstream agents must provide to hand off tasks to this agent.
    /// If set, incoming handoff requests must include this key in the X-Agent-Key header.
    /// </summary>
    public string? InboundApiKey { get; set; }

    /// <summary>Timeout in seconds for downstream agent calls. Default: 120.</summary>
    public int DownstreamTimeoutSeconds { get; set; } = 120;

    /// <summary>Port this agent listens on.</summary>
    public int Port { get; set; } = 5100;

    /// <summary>
    /// When true, after processing locally this agent will automatically hand off
    /// the result to the downstream agent. When false, handoff must be triggered explicitly.
    /// </summary>
    public bool AutoHandoff { get; set; } = true;

    public static OrchestratorOptions FromEnvironment()
    {
        var agentId = Environment.GetEnvironmentVariable("AGENT_ID");
        if (string.IsNullOrWhiteSpace(agentId))
            throw new InvalidOperationException("AGENT_ID environment variable is required.");

        var options = new OrchestratorOptions
        {
            AgentId = agentId,
            AgentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? agentId,
            DownstreamAgentUrl = Environment.GetEnvironmentVariable("DOWNSTREAM_AGENT_URL"),
            DownstreamAgentApiKey = Environment.GetEnvironmentVariable("DOWNSTREAM_AGENT_API_KEY"),
            InboundApiKey = Environment.GetEnvironmentVariable("INBOUND_API_KEY"),
            AutoHandoff = !string.Equals(
                Environment.GetEnvironmentVariable("AUTO_HANDOFF"), "false",
                StringComparison.OrdinalIgnoreCase),
        };

        if (int.TryParse(Environment.GetEnvironmentVariable("DOWNSTREAM_TIMEOUT_SECONDS"), out var timeout))
            options.DownstreamTimeoutSeconds = timeout;

        if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
            options.Port = port;

        return options;
    }
}
