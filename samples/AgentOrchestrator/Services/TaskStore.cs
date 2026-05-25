using System.Collections.Concurrent;
using AgentOrchestrator.Models;

namespace AgentOrchestrator.Services;

/// <summary>
/// In-memory store for A2A tasks. Tracks task state across their lifecycle.
/// </summary>
public sealed class TaskStore
{
    private readonly ConcurrentDictionary<string, A2ATask> _tasks = new();

    public A2ATask Create(A2AMessage initialMessage, Dictionary<string, string>? metadata = null)
    {
        var task = new A2ATask
        {
            Id = Guid.NewGuid().ToString(),
            Status = new A2ATaskStatus
            {
                State = A2ATaskState.Submitted,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            },
            Messages = [initialMessage],
            Metadata = metadata,
        };

        _tasks[task.Id] = task;
        return task;
    }

    public A2ATask? Get(string id) => _tasks.GetValueOrDefault(id);

    public void Update(A2ATask task) => _tasks[task.Id] = task;

    public bool Cancel(string id)
    {
        if (!_tasks.TryGetValue(id, out var task))
            return false;

        if (task.Status.State is A2ATaskState.Completed or A2ATaskState.Failed or A2ATaskState.Canceled)
            return false;

        task.Status = new A2ATaskStatus
        {
            State = A2ATaskState.Canceled,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };
        return true;
    }
}
