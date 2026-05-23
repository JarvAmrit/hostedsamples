namespace AgentOrchestrator.Models;

/// <summary>
/// Represents a task being passed between agents in the orchestration pipeline.
/// </summary>
public sealed class TaskPayload
{
    /// <summary>Unique identifier for this task across the orchestration chain.</summary>
    public string TaskId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The original user request or instruction.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>Accumulated context/results from previous agents in the chain.</summary>
    public List<AgentStepResult> History { get; set; } = [];

    /// <summary>Optional metadata passed along the chain (key-value pairs).</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>The identifier of the agent that sent this task.</summary>
    public string? SourceAgentId { get; set; }

    /// <summary>Timestamp when the task was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents the result of a single agent's processing step.
/// </summary>
public sealed class AgentStepResult
{
    /// <summary>Identifier of the agent that produced this result.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>The agent's output/response.</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>Status of the step (completed, failed, skipped).</summary>
    public string Status { get; set; } = "completed";

    /// <summary>When this step was completed.</summary>
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
