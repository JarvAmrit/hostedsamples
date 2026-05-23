namespace AgentOrchestrator.Models;

/// <summary>
/// Request to hand off a task to this agent from an upstream agent.
/// This is the contract for the /api/tasks/receive endpoint.
/// </summary>
public sealed class HandoffRequest
{
    /// <summary>The task payload being handed off.</summary>
    public TaskPayload Task { get; set; } = new();

    /// <summary>Optional callback URL to post the result back to the caller.</summary>
    public string? CallbackUrl { get; set; }
}
