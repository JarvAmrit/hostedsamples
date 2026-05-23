namespace HostedAgent.Models;

/// <summary>Request body for a chat interaction.</summary>
public sealed class ChatRequest
{
    /// <summary>
    /// The user message to send to the agent.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional thread ID to continue an existing conversation.
    /// When null or empty, a new thread is created.
    /// </summary>
    public string? ThreadId { get; set; }
}
