namespace HostedAgent.Configuration;

/// <summary>
/// Supported tool types that the hosted agent framework can resolve.
/// </summary>
public enum ToolType
{
    /// <summary>Bing Grounding Search – requires TOOL_N_CONNECTION (Bing connection ID).</summary>
    BingGrounding,

    /// <summary>Code Interpreter – no extra variables required.</summary>
    CodeInterpreter,

    /// <summary>File Search (vector store) – no extra variables required for basic use.</summary>
    FileSearch,

    /// <summary>
    /// Azure AI Search – requires TOOL_N_CONNECTION (AI connection ID) and
    /// TOOL_N_INDEX_NAME. Optionally TOOL_N_TOP_K, TOOL_N_FILTER, TOOL_N_QUERY_TYPE.
    /// </summary>
    AzureAiSearch,

    /// <summary>
    /// Model Context Protocol (MCP) server – requires TOOL_N_NAME (label) and
    /// TOOL_N_URL (server URL). Optionally TOOL_N_ALLOWED_TOOLS,
    /// TOOL_N_HEADERS (key=value pairs separated by |),
    /// TOOL_N_REQUIRE_APPROVAL (never|always, default: never).
    /// </summary>
    Mcp,

    /// <summary>Bing Custom Search – requires TOOL_N_CONNECTION (connection ID).</summary>
    BingCustomSearch,

    /// <summary>SharePoint – requires TOOL_N_CONNECTION (connection ID).</summary>
    SharePoint,

    /// <summary>Microsoft Fabric – requires TOOL_N_CONNECTION (connection ID).</summary>
    Fabric,

    /// <summary>Browser Automation – no extra variables required.</summary>
    BrowserAutomation,
}

/// <summary>
/// Configuration for a single tool, parsed from TOOL_&lt;N&gt;_* environment variables.
/// </summary>
public sealed class ToolOptions
{
    /// <summary>One-based index of this tool (from the env-var name).</summary>
    public int Index { get; init; }

    /// <summary>Resolved tool type.</summary>
    public ToolType Type { get; init; }

    // ── Shared ───────────────────────────────────────────────────────────────

    /// <summary>Connection ID / resource URI used by several tool types.</summary>
    public string? Connection { get; init; }

    // ── MCP ──────────────────────────────────────────────────────────────────

    /// <summary>MCP server label (also used as the tool resource key).</summary>
    public string? McpName { get; init; }

    /// <summary>MCP server URL (SSE endpoint).</summary>
    public string? McpUrl { get; init; }

    /// <summary>
    /// Comma-separated list of tool names the agent is allowed to call on this MCP server.
    /// When empty, all tools are allowed.
    /// </summary>
    public IReadOnlyList<string> McpAllowedTools { get; init; } = [];

    /// <summary>
    /// Key/value HTTP headers injected when calling the MCP server.
    /// Parsed from TOOL_N_HEADERS in "Key=Value|Key2=Value2" format.
    /// </summary>
    public IReadOnlyDictionary<string, string> McpHeaders { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// MCP approval policy. "never" = auto-approve (default), "always" = require human approval.
    /// </summary>
    public string McpRequireApproval { get; init; } = "never";

    /// <summary>
    /// Bing Custom Search instance name.
    /// </summary>
    public string? BingCustomSearchInstanceName { get; init; }

    /// <summary>
    /// Browser Automation connection ID.
    /// </summary>
    public string? BrowserConnectionId { get; init; }

    // ── Azure AI Search ───────────────────────────────────────────────────────

    /// <summary>Azure AI Search index name.</summary>
    public string? AiSearchIndexName { get; init; }

    /// <summary>Max results to return (top-k). Default: 5.</summary>
    public int AiSearchTopK { get; init; } = 5;

    /// <summary>OData filter expression.</summary>
    public string? AiSearchFilter { get; init; }

    /// <summary>Query type string: simple | semantic | vector | vector_simple_hybrid | vector_semantic_hybrid.</summary>
    public string AiSearchQueryType { get; init; } = "simple";

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans environment variables for TOOL_1_TYPE, TOOL_2_TYPE, … and returns
    /// a configuration object for each one found, in order.
    /// Scanning stops at the first gap (missing index).
    /// </summary>
    public static IReadOnlyList<ToolOptions> ReadFromEnvironment()
    {
        var list = new List<ToolOptions>();

        for (int n = 1; ; n++)
        {
            var typeStr = Environment.GetEnvironmentVariable($"TOOL_{n}_TYPE");
            if (string.IsNullOrWhiteSpace(typeStr))
                break;

            if (!TryParseToolType(typeStr, out var toolType))
            {
                Console.Error.WriteLine(
                    $"[HostedAgent] Warning: Unknown TOOL_{n}_TYPE='{typeStr}' – skipping.");
                continue;
            }

            list.Add(new ToolOptions
            {
                Index              = n,
                Type               = toolType,
                Connection         = Env($"TOOL_{n}_CONNECTION"),
                McpName            = Env($"TOOL_{n}_NAME"),
                McpUrl             = Env($"TOOL_{n}_URL"),
                McpAllowedTools    = ParseCsv(Env($"TOOL_{n}_ALLOWED_TOOLS")),
                McpHeaders         = ParseHeaders(Env($"TOOL_{n}_HEADERS")),
                McpRequireApproval = Env($"TOOL_{n}_REQUIRE_APPROVAL") ?? "never",
                AiSearchIndexName  = Env($"TOOL_{n}_INDEX_NAME"),
                AiSearchTopK       = int.TryParse(Env($"TOOL_{n}_TOP_K"), out var k) ? k : 5,
                AiSearchFilter     = Env($"TOOL_{n}_FILTER"),
                AiSearchQueryType  = Env($"TOOL_{n}_QUERY_TYPE") ?? "simple",
                BingCustomSearchInstanceName = Env($"TOOL_{n}_INSTANCE_NAME"),
                BrowserConnectionId = Env($"TOOL_{n}_BROWSER_CONNECTION") ?? Env($"TOOL_{n}_CONNECTION"),
            });
        }

        return list.AsReadOnly();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name);

    private static IReadOnlyList<string> ParseCsv(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyDictionary<string, string> ParseHeaders(string? value)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return dict;

        foreach (var pair in value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx > 0)
                dict[pair[..idx].Trim()] = pair[(idx + 1)..].Trim();
        }
        return dict;
    }

    private static bool TryParseToolType(string raw, out ToolType result)
    {
        result = raw.ToLowerInvariant().Replace("_", "").Replace("-", "") switch
        {
            "binggrounding"    => ToolType.BingGrounding,
            "codeinterpreter"  => ToolType.CodeInterpreter,
            "filesearch"       => ToolType.FileSearch,
            "azureaisearch"    => ToolType.AzureAiSearch,
            "mcp"              => ToolType.Mcp,
            "bingcustomsearch" => ToolType.BingCustomSearch,
            "sharepoint"       => ToolType.SharePoint,
            "fabric"           => ToolType.Fabric,
            "browserautomation"=> ToolType.BrowserAutomation,
            _ => (ToolType)(-1),
        };
        return (int)result >= 0;
    }
}
