namespace HostedAgent.Models;

/// <summary>A single citation returned by the agent (e.g. from Bing Grounding).</summary>
public sealed class CitationInfo
{
    public string Title { get; set; } = string.Empty;
    public string Uri   { get; set; } = string.Empty;
}

/// <summary>Response from a chat interaction.</summary>
public sealed class ChatResponse
{
    /// <summary>Thread ID – pass this back in subsequent requests to continue the conversation.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>The agent's text response.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>Citations extracted from the agent's response annotations.</summary>
    public List<CitationInfo> Citations { get; set; } = [];

    /// <summary>Final run status (completed, failed, cancelled, expired).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Error message when Status is not "completed".</summary>
    public string? Error { get; set; }
}
