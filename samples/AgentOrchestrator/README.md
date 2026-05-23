# Agent Orchestrator – Plug & Play Framework

A lightweight, plug-and-play framework for chaining hosted agents together. Each agent in the chain processes a task locally, then hands it off to the next agent via HTTP.

## Architecture

```
┌──────────────┐      ┌──────────────┐      ┌──────────────┐
│  Agent Alpha │─────▶│  Agent Beta  │─────▶│  Agent Gamma │
│  (process)   │      │  (process)   │      │  (terminal)  │
└──────────────┘      └──────────────┘      └──────────────┘
     submit              receive/process        receive/process
                         handoff                (no downstream)
```

Each agent:
1. **Receives** a task (via `/api/tasks/submit` or `/api/tasks/receive`)
2. **Processes** it locally (override `TaskProcessor` for custom logic)
3. **Hands off** to the downstream agent (if `DOWNSTREAM_AGENT_URL` is set and `AUTO_HANDOFF=true`)

## Quick Start

### 1. Configure Environment

Copy `.env.example` to `.env` and set your values:

```bash
cp .env.example .env
```

### 2. Run

```bash
cd samples/AgentOrchestrator
dotnet run
```

### 3. Multi-Agent Chain Example

Run two agents locally:

**Terminal 1 – Agent Alpha (port 5100, hands off to Beta):**
```bash
AGENT_ID=agent-alpha AGENT_NAME="Agent Alpha" PORT=5100 \
  DOWNSTREAM_AGENT_URL=http://localhost:5101 \
  dotnet run --urls http://localhost:5100
```

**Terminal 2 – Agent Beta (port 5101, terminal agent):**
```bash
AGENT_ID=agent-beta AGENT_NAME="Agent Beta" PORT=5101 \
  dotnet run --urls http://localhost:5101
```

**Submit a task to Alpha:**
```bash
curl -X POST http://localhost:5100/api/tasks/submit \
  -H "Content-Type: application/json" \
  -d '{"input": "Summarize the latest sales report"}'
```

The task flows: Alpha processes → hands off to Beta → Beta processes → returns final result through the chain.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/health` | Health check with agent info |
| POST | `/api/tasks/submit` | Submit a new task for orchestration |
| POST | `/api/tasks/receive` | Receive a handoff from an upstream agent |
| POST | `/api/tasks/handoff` | Explicitly hand off without local processing |

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `AGENT_ID` | ✅ | Unique identifier for this agent |
| `AGENT_NAME` | | Display name (defaults to AGENT_ID) |
| `DOWNSTREAM_AGENT_URL` | | URL of the next agent in the chain |
| `DOWNSTREAM_AGENT_API_KEY` | | API key for downstream authentication |
| `DOWNSTREAM_TIMEOUT_SECONDS` | | Timeout for downstream calls (default: 120) |
| `INBOUND_API_KEY` | | API key required from upstream agents |
| `AUTO_HANDOFF` | | Auto-forward to downstream (default: true) |
| `PORT` | | Listen port (default: 5100) |

## Security

- Set `INBOUND_API_KEY` on each agent to require upstream agents to authenticate via `X-Agent-Key` header.
- Set `DOWNSTREAM_AGENT_API_KEY` to authenticate when calling the downstream agent.

## Customization

Override `TaskProcessor.ProcessAsync()` to implement your own logic:

```csharp
public class MyCustomProcessor : TaskProcessor
{
    public MyCustomProcessor(OrchestratorOptions options, ILogger<TaskProcessor> logger)
        : base(options, logger) { }

    public override async Task<AgentStepResult> ProcessAsync(TaskPayload task, CancellationToken ct)
    {
        // Call your hosted agent, run business logic, etc.
        var output = await CallMyHostedAgent(task.Input);

        return new AgentStepResult
        {
            AgentId = options.AgentId,
            Output = output,
            Status = "completed",
            CompletedAt = DateTimeOffset.UtcNow,
        };
    }
}
```

Then register it in `Program.cs`:
```csharp
builder.Services.AddSingleton<TaskProcessor, MyCustomProcessor>();
```

## Integration with Hosted Agent

To connect this orchestrator to the existing `HostedAgent` sample, set up your `TaskProcessor` to call the hosted agent's `/api/chat` endpoint:

```csharp
public override async Task<AgentStepResult> ProcessAsync(TaskPayload task, CancellationToken ct)
{
    var client = new HttpClient();
    var response = await client.PostAsJsonAsync(
        "http://localhost:5000/api/chat",
        new { message = task.Input });

    var chatResult = await response.Content.ReadFromJsonAsync<ChatResponse>(ct);

    return new AgentStepResult
    {
        AgentId = _options.AgentId,
        Output = chatResult?.Response ?? "No response",
        Status = "completed",
    };
}
```
