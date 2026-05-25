namespace AgentOrchestrator.Configuration;

/// <summary>
/// Configuration loaded from environment variables for the A2A agent.
/// </summary>
public sealed class OrchestratorOptions
{
    /// <summary>Unique identifier for this agent.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Display name for this agent (published in AgentCard).</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>Description of what this agent does (published in AgentCard).</summary>
    public string? AgentDescription { get; set; }

    /// <summary>Organization name (published in AgentCard).</summary>
    public string? Organization { get; set; }

    /// <summary>
    /// URL of the downstream A2A agent to hand off tasks to.
    /// If empty, this agent is the terminal agent in the chain.
    /// The downstream agent must expose /a2a (JSON-RPC) and /.well-known/agent.json.
    /// </summary>
    public string? DownstreamAgentUrl { get; set; }

    /// <summary>
    /// Optional Bearer token for authenticating with the downstream agent.
    /// </summary>
    public string? DownstreamAgentApiKey { get; set; }

    /// <summary>Timeout in seconds for downstream agent calls. Default: 120.</summary>
    public int DownstreamTimeoutSeconds { get; set; } = 120;

    /// <summary>Port this agent listens on.</summary>
    public int Port { get; set; } = 5100;

    /// <summary>
    /// When true, after processing locally this agent will automatically hand off
    /// the task to the downstream agent via A2A message/send.
    /// </summary>
    public bool AutoHandoff { get; set; } = true;

    /// <summary>
    /// Agent skills (id → description) published in the AgentCard.
    /// Parsed from AGENT_SKILLS env var as "id1:desc1;id2:desc2".
    /// </summary>
    public Dictionary<string, string> Skills { get; set; } = [];

    public static OrchestratorOptions FromEnvironment()
    {
        var agentId = Environment.GetEnvironmentVariable("AGENT_ID");
        if (string.IsNullOrWhiteSpace(agentId))
            throw new InvalidOperationException("AGENT_ID environment variable is required.");

        var options = new OrchestratorOptions
        {
            AgentId = agentId,
            AgentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? agentId,
            AgentDescription = Environment.GetEnvironmentVariable("AGENT_DESCRIPTION"),
            Organization = Environment.GetEnvironmentVariable("AGENT_ORGANIZATION"),
            DownstreamAgentUrl = Environment.GetEnvironmentVariable("DOWNSTREAM_AGENT_URL"),
            DownstreamAgentApiKey = Environment.GetEnvironmentVariable("DOWNSTREAM_AGENT_API_KEY"),
            AutoHandoff = !string.Equals(
                Environment.GetEnvironmentVariable("AUTO_HANDOFF"), "false",
                StringComparison.OrdinalIgnoreCase),
        };

        if (int.TryParse(Environment.GetEnvironmentVariable("DOWNSTREAM_TIMEOUT_SECONDS"), out var timeout))
            options.DownstreamTimeoutSeconds = timeout;

        if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
            options.Port = port;

        // Parse skills: "summarize:Summarizes text;translate:Translates content"
        var skillsEnv = Environment.GetEnvironmentVariable("AGENT_SKILLS");
        if (!string.IsNullOrWhiteSpace(skillsEnv))
        {
            foreach (var entry in skillsEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':', 2);
                if (parts.Length == 2)
                    options.Skills[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return options;
    }
}
