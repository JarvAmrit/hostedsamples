using Azure.AI.Agents.Persistent;
using HostedAgent.Configuration;

namespace HostedAgent.Services;

/// <summary>
/// Translates <see cref="ToolOptions"/> objects (from environment variables) into
/// Azure AI Agents Persistent SDK <see cref="ToolDefinition"/> instances and the
/// matching <see cref="ToolResources"/> objects needed at agent-creation time and
/// at run-creation time.
/// </summary>
public sealed class ToolResolver
{
    private readonly IReadOnlyList<ToolOptions> _toolOptions;
    private readonly ILogger<ToolResolver> _logger;

    public ToolResolver(AgentOptions options, ILogger<ToolResolver> logger)
    {
        _toolOptions = options.Tools;
        _logger = logger;
    }

    /// <summary>
    /// Resolves all configured tools.
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    ///   <item><c>Tools</c> – ToolDefinition list to pass to <c>CreateAgentAsync</c>.</item>
    ///   <item><c>AgentResources</c> – ToolResources to pass to <c>CreateAgentAsync</c>
    ///         (e.g. AzureAISearch index bindings).</item>
    ///   <item><c>RunResources</c> – ToolResources to pass to each <c>CreateRunAsync</c>
    ///         (e.g. MCP server headers).</item>
    /// </list>
    /// </returns>
    public ResolvedTools Resolve()
    {
        var tools          = new List<ToolDefinition>();
        ToolResources?     agentResources = null;
        ToolResources?     runResources   = null;

        foreach (var opt in _toolOptions)
        {
            switch (opt.Type)
            {
                case ToolType.BingGrounding:
                    tools.Add(BuildBingGrounding(opt));
                    break;

                case ToolType.CodeInterpreter:
                    tools.Add(new CodeInterpreterToolDefinition());
                    _logger.LogInformation("Tool {Index}: CodeInterpreter enabled.", opt.Index);
                    break;

                case ToolType.FileSearch:
                    tools.Add(new FileSearchToolDefinition());
                    _logger.LogInformation("Tool {Index}: FileSearch enabled.", opt.Index);
                    break;

                case ToolType.AzureAiSearch:
                    var (aiSearchTool, aiSearchResource) = BuildAzureAiSearch(opt);
                    tools.Add(aiSearchTool);
                    agentResources ??= new ToolResources();
                    agentResources.AzureAISearch = aiSearchResource;
                    break;

                case ToolType.Mcp:
                    var (mcpTool, mcpResource) = BuildMcp(opt);
                    tools.Add(mcpTool);
                    // MCP resources go to the run, not the agent definition
                    if (runResources is null)
                        runResources = mcpResource.ToToolResources();
                    else
                        runResources.Mcp.Add(mcpResource);
                    break;

                case ToolType.BingCustomSearch:
                    tools.Add(BuildBingCustomSearch(opt));
                    break;

                case ToolType.SharePoint:
                    tools.Add(BuildSharePoint(opt));
                    break;

                case ToolType.Fabric:
                    tools.Add(BuildFabric(opt));
                    break;

                case ToolType.BrowserAutomation:
                    tools.Add(BuildBrowserAutomation(opt));
                    break;

                default:
                    _logger.LogWarning("Tool {Index}: Unhandled type '{Type}'.", opt.Index, opt.Type);
                    break;
            }
        }

        return new ResolvedTools(tools, agentResources, runResources);
    }

    // ── Tool builders ─────────────────────────────────────────────────────────

    private BingGroundingToolDefinition BuildBingGrounding(ToolOptions opt)
    {
        RequireConnection(opt, "BingGrounding");
        _logger.LogInformation(
            "Tool {Index}: BingGrounding using connection '{Connection}'.",
            opt.Index, opt.Connection);

        return new BingGroundingToolDefinition(
            new BingGroundingSearchToolParameters(
                [new BingGroundingSearchConfiguration(opt.Connection!)]));
    }

    private (AzureAISearchToolDefinition tool, AzureAISearchToolResource resource)
        BuildAzureAiSearch(ToolOptions opt)
    {
        RequireConnection(opt, "AzureAiSearch");
        if (string.IsNullOrWhiteSpace(opt.AiSearchIndexName))
            throw new InvalidOperationException(
                $"TOOL_{opt.Index}_INDEX_NAME is required for AzureAiSearch.");

        var queryType = opt.AiSearchQueryType.ToLowerInvariant() switch
        {
            "semantic"               => AzureAISearchQueryType.Semantic,
            "vector"                 => AzureAISearchQueryType.Vector,
            "vectorsimplehybrid"     => AzureAISearchQueryType.VectorSimpleHybrid,
            "vectorsemantichybrid"   => AzureAISearchQueryType.VectorSemanticHybrid,
            _                        => AzureAISearchQueryType.Simple,
        };

        _logger.LogInformation(
            "Tool {Index}: AzureAiSearch index='{Index}' queryType={QueryType} topK={TopK}.",
            opt.Index, opt.AiSearchIndexName, queryType, opt.AiSearchTopK);

        var resource = new AzureAISearchToolResource(
            opt.Connection!,
            opt.AiSearchIndexName,
            opt.AiSearchTopK,
            opt.AiSearchFilter,
            queryType);

        return (new AzureAISearchToolDefinition(), resource);
    }

    private (MCPToolDefinition tool, MCPToolResource resource) BuildMcp(ToolOptions opt)
    {
        if (string.IsNullOrWhiteSpace(opt.McpName))
            throw new InvalidOperationException(
                $"TOOL_{opt.Index}_NAME is required for MCP.");
        if (string.IsNullOrWhiteSpace(opt.McpUrl))
            throw new InvalidOperationException(
                $"TOOL_{opt.Index}_URL is required for MCP.");

        _logger.LogInformation(
            "Tool {Index}: MCP server='{Name}' url='{Url}' approval='{Approval}'.",
            opt.Index, opt.McpName, opt.McpUrl, opt.McpRequireApproval);

        var toolDef = new MCPToolDefinition(opt.McpName, opt.McpUrl);
        foreach (var allowed in opt.McpAllowedTools)
            toolDef.AllowedTools.Add(allowed);

        var resource = new MCPToolResource(opt.McpName);
        resource.RequireApproval = new MCPApproval(opt.McpRequireApproval);

        foreach (var (key, value) in opt.McpHeaders)
            resource.UpdateHeader(key, value);

        return (toolDef, resource);
    }

    private BingCustomSearchToolDefinition BuildBingCustomSearch(ToolOptions opt)
    {
        RequireConnection(opt, "BingCustomSearch");
        _logger.LogInformation(
            "Tool {Index}: BingCustomSearch using connection '{Connection}', instance '{Instance}'.",
            opt.Index, opt.Connection, opt.BingCustomSearchInstanceName ?? "(none)");

        BingCustomSearchToolParameters parameters = string.IsNullOrWhiteSpace(opt.BingCustomSearchInstanceName)
            ? new BingCustomSearchToolParameters(
                [new BingCustomSearchConfiguration(opt.Connection!, string.Empty)])
            : new BingCustomSearchToolParameters(opt.Connection!, opt.BingCustomSearchInstanceName);

        return new BingCustomSearchToolDefinition(parameters);
    }

    private SharepointToolDefinition BuildSharePoint(ToolOptions opt)
    {
        RequireConnection(opt, "SharePoint");
        _logger.LogInformation(
            "Tool {Index}: SharePoint using connection '{Connection}'.",
            opt.Index, opt.Connection);

        return new SharepointToolDefinition(new SharepointGroundingToolParameters(opt.Connection!));
    }

    private MicrosoftFabricToolDefinition BuildFabric(ToolOptions opt)
    {
        RequireConnection(opt, "Fabric");
        _logger.LogInformation(
            "Tool {Index}: Fabric using connection '{Connection}'.",
            opt.Index, opt.Connection);

        return new MicrosoftFabricToolDefinition(new FabricDataAgentToolParameters(opt.Connection!));
    }

    private BrowserAutomationToolDefinition BuildBrowserAutomation(ToolOptions opt)
    {
        if (string.IsNullOrWhiteSpace(opt.BrowserConnectionId))
            throw new InvalidOperationException(
                $"TOOL_{opt.Index}_CONNECTION (or TOOL_{opt.Index}_BROWSER_CONNECTION) is required for BrowserAutomation.");
        _logger.LogInformation(
            "Tool {Index}: BrowserAutomation using connection '{Connection}'.",
            opt.Index, opt.BrowserConnectionId);

        return new BrowserAutomationToolDefinition(
            new BrowserAutomationToolParameters(
                new BrowserAutomationToolConnectionParameters(opt.BrowserConnectionId!)));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static void RequireConnection(ToolOptions opt, string toolName)
    {
        if (string.IsNullOrWhiteSpace(opt.Connection))
            throw new InvalidOperationException(
                $"TOOL_{opt.Index}_CONNECTION is required for {toolName}.");
    }
}

/// <summary>
/// Result of <see cref="ToolResolver.Resolve"/>.
/// </summary>
public sealed record ResolvedTools(
    IReadOnlyList<ToolDefinition> Tools,
    ToolResources? AgentResources,
    ToolResources? RunResources);
