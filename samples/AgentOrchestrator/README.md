# A2A Agent – Agent-to-Agent Protocol Sample

An agent that implements the [A2A (Agent-to-Agent) protocol](https://a2a-protocol.org/) — the open standard for interoperable agent communication using JSON-RPC 2.0 over HTTP.

## What is A2A?

The Agent-to-Agent protocol enables agents built by different teams/frameworks to securely delegate tasks to each other. It uses:
- **JSON-RPC 2.0** as the transport layer
- **AgentCard** for capability discovery (`/.well-known/agent.json`)
- **Standard task lifecycle**: submitted → working → completed/failed/canceled
- **Multi-modal content**: text, files, structured data via typed Parts

## Architecture

```
┌──────────────────┐                    ┌──────────────────┐
│   Agent Alpha    │   A2A message/send │   Agent Beta     │
│                  │───────────────────▶│                  │
│  POST /a2a       │                    │  POST /a2a       │
│  /.well-known/   │◀───────────────────│  /.well-known/   │
│   agent.json     │   A2A Task result  │   agent.json     │
└──────────────────┘                    └──────────────────┘
```

Each agent:
1. **Publishes** an AgentCard at `/.well-known/agent.json` (discovery)
2. **Receives** A2A JSON-RPC requests at `POST /a2a` (any agent can send tasks)
3. **Processes** the message locally
4. **Hands off** to downstream agents via A2A `message/send` (if configured)

## Quick Start

### 1. Configure Environment

```bash
cp .env.example .env
```

### 2. Run

```bash
cd samples/AgentOrchestrator
dotnet run
```

### 3. Multi-Agent Chain Example

**Terminal 1 – Agent Alpha (hands off to Beta):**
```bash
AGENT_ID=alpha AGENT_NAME="Agent Alpha" \
  DOWNSTREAM_AGENT_URL=http://localhost:5101 \
  dotnet run --urls http://localhost:5100
```

**Terminal 2 – Agent Beta (terminal agent):**
```bash
AGENT_ID=beta AGENT_NAME="Agent Beta" \
  dotnet run --urls http://localhost:5101
```

**Discover Agent Alpha's capabilities:**
```bash
curl http://localhost:5100/.well-known/agent.json | jq
```

**Send an A2A message (task handoff):**
```bash
curl -X POST http://localhost:5100/a2a \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "req-1",
    "method": "message/send",
    "params": {
      "message": {
        "role": "user",
        "parts": [{"type": "text", "text": "Summarize the latest sales report"}]
      }
    }
  }'
```

**Get task status:**
```bash
curl -X POST http://localhost:5100/a2a \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "req-2",
    "method": "tasks/get",
    "params": {"id": "<task-id-from-previous-response>"}
  }'
```

The task flows: Alpha receives → processes → hands off to Beta via A2A → Beta processes → result returns through the chain.

## A2A Protocol Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/a2a` | JSON-RPC 2.0 endpoint (message/send, tasks/get, tasks/cancel) |
| GET | `/.well-known/agent.json` | AgentCard discovery |
| GET | `/api/health` | Health check |

## JSON-RPC Methods

| Method | Description |
|--------|-------------|
| `message/send` | Send a message to create/continue a task |
| `tasks/get` | Get the current state of a task by ID |
| `tasks/cancel` | Cancel an in-progress task |

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `AGENT_ID` | ✅ | Unique identifier for this agent |
| `AGENT_NAME` | | Display name (defaults to AGENT_ID) |
| `AGENT_DESCRIPTION` | | Agent description (published in AgentCard) |
| `AGENT_ORGANIZATION` | | Organization name (published in AgentCard) |
| `AGENT_SKILLS` | | Skills in "id:description;id:description" format |
| `DOWNSTREAM_AGENT_URL` | | URL of the downstream A2A agent |
| `DOWNSTREAM_AGENT_API_KEY` | | Bearer token for downstream auth |
| `DOWNSTREAM_TIMEOUT_SECONDS` | | Timeout for downstream calls (default: 120) |
| `AUTO_HANDOFF` | | Auto-forward to downstream (default: true) |
| `PORT` | | Listen port (default: 5100) |

## Receiving Any Content

This agent can receive **any** A2A-compliant message — text, files (base64 or URI), or structured data. The `message/send` method accepts messages with multiple parts of different types:

```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "message/send",
  "params": {
    "message": {
      "role": "user",
      "parts": [
        {"type": "text", "text": "Process this document"},
        {"type": "file", "file": {"name": "report.pdf", "mimeType": "application/pdf", "bytes": "<base64>"}},
        {"type": "data", "data": {"key": "value", "numbers": [1, 2, 3]}}
      ]
    }
  }
}
```

## Security

- Set `DOWNSTREAM_AGENT_API_KEY` to send a Bearer token when calling downstream agents.
- Agents can validate tokens in their own middleware as needed.

## Interoperability

Any A2A-compliant agent (Python, JavaScript, Java, etc.) can communicate with this agent — just point it at the `/a2a` endpoint. The protocol is framework-agnostic.

## References

- [A2A Protocol Specification](https://a2a-protocol.org/latest/specification/)
- [A2A GitHub Repository](https://github.com/a2aproject/A2A)
- [Google Codelabs: Getting Started with A2A](https://codelabs.developers.google.com/intro-a2a-purchasing-concierge)
