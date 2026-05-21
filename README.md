# Hosted Agent – Plug-and-Play Azure AI Foundry Agent Framework

A **Docker-containerisable, environment-variable-driven** AI agent built on the
[Azure AI Agents Persistent SDK](https://www.nuget.org/packages/Azure.AI.Agents.Persistent/)
for .NET.  
Drop in your system prompt and tool configuration via environment variables, run the
container, and you have a ready-made conversational agent API — no code changes required.

---

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
- [Environment Variables Reference](#environment-variables-reference)
  - [Core Variables](#core-variables)
  - [Authentication](#authentication)
  - [Agent Behaviour](#agent-behaviour)
  - [Tool Configuration](#tool-configuration)
- [Supported Tools](#supported-tools)
- [REST API](#rest-api)
- [Multi-Turn Conversations](#multi-turn-conversations)
- [Project Structure](#project-structure)
- [Building Locally](#building-locally)

---

## Features

| Capability | Detail |
|---|---|
| **Zero-code configuration** | System prompt, model, and all tools set via env vars |
| **All major Azure AI tools** | Bing Grounding, Code Interpreter, Azure AI Search, MCP, SharePoint, Fabric, Browser Automation, File Search, Bing Custom Search |
| **MCP (Model Context Protocol)** | Multiple MCP servers, per-server allowed-tool lists, custom headers, auto-approval |
| **Multi-turn conversations** | Thread ID returned in every response; pass it back for follow-ups |
| **DefaultAzureCredential** | Supports Managed Identity, service principal, Azure CLI, and more |
| **Docker-first** | Multi-stage Dockerfile with a non-root runtime user |
| **Swagger UI** | Interactive API explorer at `/swagger` when `ENABLE_SWAGGER=true` |

---

## Quick Start

### 1. Clone and configure

```bash
git clone https://github.com/JarvAmrit/hostedsamples
cd hostedsamples
cp .env.example .env
# Edit .env with your Azure AI Foundry endpoint, model name, system prompt, and tools
```

### 2. Run with Docker Compose

```bash
docker compose up --build
```

### 3. Chat with the agent

```bash
curl -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello! What can you help me with?"}'
```

---

## Environment Variables Reference

### Core Variables

| Variable | Required | Description |
|---|---|---|
| `PROJECT_ENDPOINT` | ✅ | Azure AI Foundry project endpoint, e.g. `https://<resource>.services.ai.azure.com/api/projects/<project>` |
| `MODEL_DEPLOYMENT_NAME` | ✅ | Model deployment name, e.g. `gpt-4o` |

### Authentication

The agent uses **`DefaultAzureCredential`**, which tries these sources in order:

1. **Service principal** – set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`
2. **Workload Identity / Managed Identity** – works automatically in Azure Container Apps, AKS, etc.
3. **Azure CLI** – run `az login` before starting locally

### Agent Behaviour

| Variable | Default | Description |
|---|---|---|
| `SYSTEM_PROMPT` | `You are a helpful assistant.` | The agent's instructions / persona |
| `AGENT_NAME` | `hosted-agent` | Display name used when registering the agent in Foundry |
| `AGENT_TEMPERATURE` | _(model default)_ | Sampling temperature 0.0–2.0 |
| `AGENT_MAX_TOKENS` | _(model default)_ | Max tokens in the response |
| `AGENT_REUSE_EXISTING` | `false` | When `true`, looks for an existing agent with `AGENT_NAME` and reuses it |
| `ENABLE_SWAGGER` | `false` | When `true`, serves Swagger UI at `/swagger` |

### Tool Configuration

Tools are numbered from **1** upward. Scanning stops at the first missing `TOOL_N_TYPE`.

```
TOOL_1_TYPE=bing_grounding
TOOL_1_CONNECTION=<connection-id>

TOOL_2_TYPE=mcp
TOOL_2_NAME=my_server
TOOL_2_URL=https://mcp.example.com/sse
```

---

## Supported Tools

### `bing_grounding` – Bing Grounding Search

| Variable | Required | Description |
|---|---|---|
| `TOOL_N_CONNECTION` | ✅ | Bing connection ID from Azure AI Foundry |

### `code_interpreter` – Code Interpreter

No extra variables required.

### `file_search` – File Search

No extra variables required (attach vector stores separately via the Foundry portal or SDK).

### `azure_ai_search` – Azure AI Search

| Variable | Required | Default | Description |
|---|---|---|---|
| `TOOL_N_CONNECTION` | ✅ | | Azure AI connection ID |
| `TOOL_N_INDEX_NAME` | ✅ | | Index name |
| `TOOL_N_TOP_K` | | `5` | Number of results to return |
| `TOOL_N_QUERY_TYPE` | | `simple` | `simple` \| `semantic` \| `vector` \| `vector_simple_hybrid` \| `vector_semantic_hybrid` |
| `TOOL_N_FILTER` | | | OData filter expression |

### `mcp` – Model Context Protocol Server

| Variable | Required | Default | Description |
|---|---|---|---|
| `TOOL_N_NAME` | ✅ | | Unique label for the MCP server |
| `TOOL_N_URL` | ✅ | | MCP SSE server URL |
| `TOOL_N_ALLOWED_TOOLS` | | _(all)_ | Comma-separated list of tool names to allow |
| `TOOL_N_REQUIRE_APPROVAL` | | `never` | `never` (auto-approve) or `always` |
| `TOOL_N_HEADERS` | | | HTTP headers: `Key=Value\|Key2=Value2` |

### `bing_custom_search` – Bing Custom Search

| Variable | Required | Description |
|---|---|---|
| `TOOL_N_CONNECTION` | ✅ | Bing Custom Search connection ID |
| `TOOL_N_INSTANCE_NAME` | ✅ | Bing Custom Search instance name |

### `sharepoint` – SharePoint Grounding

| Variable | Required | Description |
|---|---|---|
| `TOOL_N_CONNECTION` | ✅ | SharePoint connection ID |

### `fabric` – Microsoft Fabric Data Agent

| Variable | Required | Description |
|---|---|---|
| `TOOL_N_CONNECTION` | ✅ | Fabric connection ID |

### `browser_automation` – Browser Automation

| Variable | Required | Description |
|---|---|---|
| `TOOL_N_CONNECTION` | ✅ | Browser Automation connection ID |

---

## REST API

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/health` | Health check |
| `GET` | `/api/agent` | Metadata about the active agent |
| `POST` | `/api/chat` | Send a message, get a reply |

### POST `/api/chat`

**Request body**

```json
{
  "message": "What is the capital of France?",
  "threadId": "thread_abc123"
}
```

| Field | Required | Description |
|---|---|---|
| `message` | ✅ | User message |
| `threadId` | | Pass the `threadId` from a previous response to continue that conversation |

**Response body**

```json
{
  "threadId": "thread_abc123",
  "response": "The capital of France is Paris.",
  "citations": [
    { "title": "France – Wikipedia", "uri": "https://en.wikipedia.org/wiki/France" }
  ],
  "status": "Completed",
  "error": null
}
```

---

## Multi-Turn Conversations

Every response includes a `threadId`. Pass it in subsequent requests to continue the same conversation:

```bash
# First turn
RESP=$(curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "My name is Alice."}')

THREAD=$(echo $RESP | jq -r '.threadId')

# Follow-up turn (same thread)
curl -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"What is my name?\", \"threadId\": \"$THREAD\"}"
```

---

## Project Structure

```
hostedsamples/
├── src/
│   └── HostedAgent/
│       ├── HostedAgent.csproj              # .NET 9 web project
│       ├── Program.cs                      # Host setup & middleware
│       ├── Configuration/
│       │   ├── AgentOptions.cs             # Core env-var configuration model
│       │   └── ToolOptions.cs              # Per-tool env-var configuration model
│       ├── Services/
│       │   ├── ToolResolver.cs             # Translates ToolOptions → SDK ToolDefinitions
│       │   └── AgentService.cs             # Agent lifecycle & chat orchestration
│       ├── Api/
│       │   └── ChatEndpoints.cs            # Minimal-API route definitions
│       └── Models/
│           ├── ChatRequest.cs
│           ├── ChatResponse.cs
│           └── AgentInfoResponse.cs
├── Dockerfile                              # Multi-stage build
├── docker-compose.yml
├── .env.example                            # Full env-var reference
└── README.md
```

---

## Building Locally

```bash
# Restore & build
cd src/HostedAgent
dotnet restore
dotnet build

# Run (requires .env or the env vars exported in your shell)
export PROJECT_ENDPOINT=https://...
export MODEL_DEPLOYMENT_NAME=gpt-4o
export SYSTEM_PROMPT="You are a helpful assistant."
dotnet run
```

The API will be available at `http://localhost:5000` (or the port shown in the console).
