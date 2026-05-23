namespace HostedAgent.Models;

/// <summary>Summary of the active agent returned by GET /agent.</summary>
public sealed class AgentInfoResponse
{
    public string AgentId      { get; set; } = string.Empty;
    public string AgentName    { get; set; } = string.Empty;
    public string Model        { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<string> Tools  { get; set; } = [];
}
