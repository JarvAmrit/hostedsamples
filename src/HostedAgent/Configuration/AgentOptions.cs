namespace HostedAgent.Configuration;

/// <summary>
/// Root configuration for the hosted agent, bound from environment variables.
/// </summary>
public sealed class AgentOptions
{
    // ── Azure AI Foundry connection ──────────────────────────────────────────

    /// <summary>
    /// Azure AI Foundry project endpoint.
    /// Environment variable: PROJECT_ENDPOINT
    /// Example: https://&lt;resource&gt;.services.ai.azure.com/api/projects/&lt;project&gt;
    /// </summary>
    public string ProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Model deployment name (e.g. gpt-4o, gpt-4.1).
    /// Environment variable: MODEL_DEPLOYMENT_NAME
    /// </summary>
    public string ModelDeploymentName { get; set; } = string.Empty;

    // ── Agent behaviour ──────────────────────────────────────────────────────

    /// <summary>
    /// System prompt / instructions given to the agent.
    /// Environment variable: SYSTEM_PROMPT
    /// </summary>
    public string SystemPrompt { get; set; } = "You are a helpful assistant.";

    /// <summary>
    /// Display name for the agent.
    /// Environment variable: AGENT_NAME
    /// </summary>
    public string AgentName { get; set; } = "hosted-agent";

    /// <summary>
    /// Optional sampling temperature (0.0 – 2.0).
    /// Environment variable: AGENT_TEMPERATURE
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Optional maximum tokens in the response.
    /// Environment variable: AGENT_MAX_TOKENS
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// When true, an existing agent with the same name will be reused instead of creating a new one.
    /// Environment variable: AGENT_REUSE_EXISTING  (default: false)
    /// </summary>
    public bool ReuseExistingAgent { get; set; } = false;

    // ── Tool configurations ──────────────────────────────────────────────────

    /// <summary>
    /// Ordered list of tool configurations parsed from TOOL_&lt;N&gt;_* environment variables.
    /// </summary>
    public IReadOnlyList<ToolOptions> Tools { get; set; } = [];

    // ── Factory ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all relevant environment variables and returns a fully populated <see cref="AgentOptions"/>.
    /// </summary>
    public static AgentOptions FromEnvironment()
    {
        var options = new AgentOptions
        {
            ProjectEndpoint    = Required("PROJECT_ENDPOINT"),
            ModelDeploymentName = Required("MODEL_DEPLOYMENT_NAME"),
            SystemPrompt       = Environment.GetEnvironmentVariable("SYSTEM_PROMPT")
                                    ?? "You are a helpful assistant.",
            AgentName          = Environment.GetEnvironmentVariable("AGENT_NAME")
                                    ?? "hosted-agent",
            ReuseExistingAgent = IsTrue("AGENT_REUSE_EXISTING"),
        };

        if (float.TryParse(
                Environment.GetEnvironmentVariable("AGENT_TEMPERATURE"),
                System.Globalization.CultureInfo.InvariantCulture,
                out float temp))
            options.Temperature = temp;

        if (int.TryParse(
                Environment.GetEnvironmentVariable("AGENT_MAX_TOKENS"),
                out int maxTok))
            options.MaxTokens = maxTok;

        options.Tools = ToolOptions.ReadFromEnvironment();

        return options;
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException(
                   $"Required environment variable '{name}' is not set.");

    private static bool IsTrue(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "1", StringComparison.OrdinalIgnoreCase);
    }
}
