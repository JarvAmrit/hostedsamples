using AgentOrchestrator.Configuration;
using AgentOrchestrator.Models;

namespace AgentOrchestrator.Services;

/// <summary>
/// Handles inbound A2A JSON-RPC requests — processes tasks and optionally hands off downstream.
/// This is the core orchestration logic that receives any A2A message and processes it.
/// </summary>
public sealed class A2AHandler
{
    private readonly OrchestratorOptions _options;
    private readonly TaskStore _taskStore;
    private readonly A2AClient _client;
    private readonly ILogger<A2AHandler> _logger;

    public A2AHandler(
        OrchestratorOptions options,
        TaskStore taskStore,
        A2AClient client,
        ILogger<A2AHandler> logger)
    {
        _options = options;
        _taskStore = taskStore;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches an inbound JSON-RPC request to the appropriate handler.
    /// </summary>
    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken ct = default)
    {
        var safeMethod = request.Method?.Replace("\n", "").Replace("\r", "") ?? "(null)";
        _logger.LogInformation("[A2AHandler] Received method: {Method}", safeMethod);

        return request.Method switch
        {
            "message/send" => await HandleMessageSend(request, ct),
            "tasks/get" => HandleTasksGet(request),
            "tasks/cancel" => HandleTasksCancel(request),
            _ => MakeError(request.Id, -32601, $"Method not found: {request.Method}"),
        };
    }

    private async Task<JsonRpcResponse> HandleMessageSend(JsonRpcRequest request, CancellationToken ct)
    {
        var message = request.Params?.Message;
        if (message == null || message.Parts.Count == 0)
        {
            return MakeError(request.Id, -32602, "Invalid params: message with at least one part is required.");
        }

        // Create task in store
        var task = _taskStore.Create(message, request.Params?.Metadata);

        // Mark as working
        task.Status = new A2ATaskStatus
        {
            State = A2ATaskState.Working,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
        };
        _taskStore.Update(task);

        // Process locally: the agent acknowledges and transforms the message
        var outputText = ProcessLocally(message);

        // Build agent response message
        var agentResponse = new A2AMessage
        {
            Role = "agent",
            Parts = [new TextPart { Text = outputText }],
        };
        task.Messages.Add(agentResponse);

        // If auto-handoff is enabled and there's a downstream agent, delegate via A2A
        if (_options.AutoHandoff && _client.HasDownstream)
        {
            _logger.LogInformation("[A2AHandler] Handing off task {TaskId} to downstream via A2A", task.Id);

            // Forward the original user message to the downstream agent
            var downstreamTask = await _client.SendMessageAsync(message, request.Params?.Metadata, ct);

            if (downstreamTask != null)
            {
                // Merge downstream result into our task
                task.Status = downstreamTask.Status;
                task.Artifacts = downstreamTask.Artifacts;

                // Add downstream messages to history
                foreach (var msg in downstreamTask.Messages.Where(m => m.Role == "agent"))
                {
                    task.Messages.Add(msg);
                }

                task.Metadata ??= new Dictionary<string, string>();
                task.Metadata["handedOffTo"] = _options.DownstreamAgentUrl ?? "";
            }
            else
            {
                // Downstream failed — complete with local result
                task.Status = new A2ATaskStatus
                {
                    State = A2ATaskState.Completed,
                    Message = agentResponse,
                    Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                };

                task.Artifacts =
                [
                    new A2AArtifact
                    {
                        Name = "response",
                        Parts = [new TextPart { Text = outputText }],
                        Index = 0,
                    }
                ];
            }
        }
        else
        {
            // Terminal agent — complete with local result
            task.Status = new A2ATaskStatus
            {
                State = A2ATaskState.Completed,
                Message = agentResponse,
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            };

            task.Artifacts =
            [
                new A2AArtifact
                {
                    Name = "response",
                    Parts = [new TextPart { Text = outputText }],
                    Index = 0,
                }
            ];
        }

        _taskStore.Update(task);

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = task,
        };
    }

    private JsonRpcResponse HandleTasksGet(JsonRpcRequest request)
    {
        var taskId = request.Params?.Id;
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return MakeError(request.Id, -32602, "Invalid params: task id is required.");
        }

        var task = _taskStore.Get(taskId);
        if (task == null)
        {
            return MakeError(request.Id, -32001, $"Task not found: {taskId}");
        }

        return new JsonRpcResponse { Id = request.Id, Result = task };
    }

    private JsonRpcResponse HandleTasksCancel(JsonRpcRequest request)
    {
        var taskId = request.Params?.Id;
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return MakeError(request.Id, -32602, "Invalid params: task id is required.");
        }

        var canceled = _taskStore.Cancel(taskId);
        if (!canceled)
        {
            return MakeError(request.Id, -32001, $"Task not found or cannot be canceled: {taskId}");
        }

        var task = _taskStore.Get(taskId)!;
        return new JsonRpcResponse { Id = request.Id, Result = task };
    }

    /// <summary>
    /// Local processing logic. Override or extend to plug in actual agent logic
    /// (e.g., call a hosted LLM, run tools, etc.).
    /// Default: echo/passthrough acknowledging receipt.
    /// </summary>
    private string ProcessLocally(A2AMessage message)
    {
        var inputText = string.Join(" ", message.Parts
            .OfType<TextPart>()
            .Select(p => p.Text));

        return $"[{_options.AgentName}] Processed: {inputText}";
    }

    private static JsonRpcResponse MakeError(string? id, int code, string message) =>
        new()
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message },
        };
}
