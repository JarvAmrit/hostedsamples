namespace AgentOrchestrator.Models;

/// <summary>
/// Final result returned after orchestration completes (all agents in the chain have processed).
/// </summary>
public sealed class TaskResult
{
    /// <summary>The task ID that was processed.</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>Overall status: completed, failed, handed_off.</summary>
    public string Status { get; set; } = "completed";

    /// <summary>Final output (from the last agent in the chain or the current agent).</summary>
    public string FinalOutput { get; set; } = string.Empty;

    /// <summary>Full history of all agent steps.</summary>
    public List<AgentStepResult> Steps { get; set; } = [];

    /// <summary>Error message if status is failed.</summary>
    public string? Error { get; set; }

    /// <summary>If the task was handed off, the URL of the downstream agent.</summary>
    public string? HandedOffTo { get; set; }
}
