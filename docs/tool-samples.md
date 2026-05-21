# Tool Call Samples

Practical, copy-paste examples for every tool supported by the Hosted Agent framework.  
Each sample shows the exact **environment variables** to set, how to **start the container**, and the **`curl` commands** to exercise the tool.

> **Tip:** Copy `.env.example` to `.env`, uncomment the relevant lines, then run `docker compose up --build`.

---

## Table of Contents

1. [MCP (Model Context Protocol)](#1-mcp-model-context-protocol)
   - [Single MCP Server](#11-single-mcp-server)
   - [Multiple MCP Servers](#12-multiple-mcp-servers)
   - [Restricting Allowed Tools](#13-restricting-which-tools-the-agent-may-call)
   - [Custom HTTP Headers](#14-custom-http-headers-for-authentication)
   - [Manual Approval Mode](#15-manual-approval-mode)
   - [Multi-Turn MCP Conversation](#16-multi-turn-mcp-conversation)
2. [Bing Grounding Search](#2-bing-grounding-search)
3. [Code Interpreter](#3-code-interpreter)
4. [File Search](#4-file-search)
5. [Azure AI Search](#5-azure-ai-search)
   - [Simple Query](#51-simple-query-default)
   - [Semantic Ranking](#52-semantic-ranking)
   - [Vector Search](#53-vector-search)
   - [Hybrid Search](#54-hybrid-search)
6. [SharePoint Grounding](#6-sharepoint-grounding)
7. [Microsoft Fabric Data Agent](#7-microsoft-fabric-data-agent)
8. [Bing Custom Search](#8-bing-custom-search)
9. [Browser Automation](#9-browser-automation)
10. [Combining Multiple Tools](#10-combining-multiple-tools)
11. [Environment Variable Quick-Reference](#11-environment-variable-quick-reference)

---

## 1. MCP (Model Context Protocol)

MCP lets you connect the agent to any **SSE-based MCP server** — internal microservices,
GitHub Copilot Extensions, third-party APIs, or your own tool servers.

### 1.1 Single MCP Server

**What it does:** Connects the agent to one MCP server that exposes tools (e.g. a weather API, an internal knowledge base, a ticketing system).

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a helpful assistant that can look up information using available tools.
AGENT_NAME=mcp-demo-agent

TOOL_1_TYPE=mcp
TOOL_1_NAME=my_tool_server
TOOL_1_URL=https://mcp.example.com/sse
TOOL_1_REQUIRE_APPROVAL=never
```

**Start the container**
```bash
docker compose up --build
```

**Send a message**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is the weather in London right now?"}' | jq
```

**Example response**
```json
{
  "threadId": "thread_abc123",
  "response": "The current weather in London is 15°C with light rain.",
  "citations": [],
  "status": "Completed",
  "error": null
}
```

> Save `threadId` from the response to continue the conversation — see [§1.6](#16-multi-turn-mcp-conversation).

---

### 1.2 Multiple MCP Servers

You can connect up to **N** MCP servers simultaneously. The agent decides which server's tools to use based on the tools' descriptions.

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a knowledgeable assistant. Use the documentation and code tools to answer questions accurately.
AGENT_NAME=multi-mcp-agent

# MCP server 1 – internal documentation search
TOOL_1_TYPE=mcp
TOOL_1_NAME=docs_server
TOOL_1_URL=https://docs-mcp.internal.example.com/sse
TOOL_1_REQUIRE_APPROVAL=never

# MCP server 2 – code execution / analysis
TOOL_2_TYPE=mcp
TOOL_2_NAME=code_server
TOOL_2_URL=https://code-mcp.internal.example.com/sse
TOOL_2_REQUIRE_APPROVAL=never
```

**Send a message that uses both servers**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Find the docs for our sort algorithm and then run the benchmark test for it."}' | jq
```

The agent automatically picks the right server (docs vs. code) for each sub-task.

---

### 1.3 Restricting Which Tools the Agent May Call

Use `TOOL_N_ALLOWED_TOOLS` to limit the agent to a **safe subset** of an MCP server's tools.  
This is useful when a server exposes both read-only and destructive operations.

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a read-only data assistant. You may search and read, but never write or delete.
AGENT_NAME=readonly-agent

TOOL_1_TYPE=mcp
TOOL_1_NAME=data_server
TOOL_1_URL=https://data-mcp.example.com/sse
TOOL_1_ALLOWED_TOOLS=search_records,get_record,list_tables
TOOL_1_REQUIRE_APPROVAL=never
```

The agent will only ever call `search_records`, `get_record`, or `list_tables`.  
Even if the model tries to invoke `delete_record`, the SDK will reject it.

---

### 1.4 Custom HTTP Headers for Authentication

Many MCP servers require authentication headers (API keys, Bearer tokens, etc.).

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a support assistant with access to our internal ticketing system.
AGENT_NAME=support-agent

TOOL_1_TYPE=mcp
TOOL_1_NAME=ticketing_server
TOOL_1_URL=https://jira-mcp.example.com/sse
TOOL_1_HEADERS=Authorization=Bearer eyJhbGciOi...|X-Workspace-Id=acme-corp
TOOL_1_REQUIRE_APPROVAL=never
```

**Header format:** `Key=Value` pairs separated by `|`.  
Special characters in values are supported (the `=` in `Bearer eyJ...` is fine because only the **first** `=` per pair is used as the separator).

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "List all open P1 bugs assigned to me."}' | jq
```

---

### 1.5 Manual Approval Mode

By default (`TOOL_N_REQUIRE_APPROVAL=never`) the framework **auto-approves** every MCP tool call.  
Set `TOOL_N_REQUIRE_APPROVAL=always` if you want the underlying Azure AI Foundry SDK to require explicit approval for each call.

> **Note:** In the current framework implementation, auto-approval is handled server-side
> using the headers supplied in `TOOL_N_HEADERS`. When set to `always`, the run will
> enter `RequiresAction` state and the `AgentService` polling loop will still approve
> them automatically using those same headers (this is the default behaviour for
> headless containers). If you want truly interactive human-in-the-loop approval, you
> would need to build a front-end that calls `POST /api/chat` incrementally and handles
> the `status: RequiresAction` response. See the [REST API docs](../README.md#rest-api)
> for the response schema.

**.env**
```env
TOOL_1_TYPE=mcp
TOOL_1_NAME=sensitive_server
TOOL_1_URL=https://sensitive-mcp.example.com/sse
TOOL_1_REQUIRE_APPROVAL=always
TOOL_1_HEADERS=Authorization=Bearer <token>
```

---

### 1.6 Multi-Turn MCP Conversation

The `threadId` in every response lets you **continue the same conversation**.

```bash
# Turn 1 – open a ticket
RESP1=$(curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Create a bug ticket: login page crashes on Safari 17."}')

echo $RESP1 | jq

THREAD=$(echo $RESP1 | jq -r '.threadId')

# Turn 2 – follow up in the same thread (agent remembers context)
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"Assign it to the frontend team and set priority to P1.\", \"threadId\": \"$THREAD\"}" | jq

# Turn 3 – ask for a summary
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"Give me a summary of what we just did.\", \"threadId\": \"$THREAD\"}" | jq
```

---

## 2. Bing Grounding Search

Lets the agent answer questions using **real-time Bing web search**, with citations.

**Prerequisites:** A Bing Grounding connection registered in Azure AI Foundry  
([How to create a Bing connection](https://learn.microsoft.com/azure/ai-services/agents/how-to/tools/bing-grounding))

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a research assistant. Always cite your sources.
AGENT_NAME=bing-agent

TOOL_1_TYPE=bing_grounding
TOOL_1_CONNECTION=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<acct>/connections/<bing-conn>
```

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What are the latest announcements from Microsoft Build 2025?"}' | jq
```

**Example response** (citations populated automatically)
```json
{
  "threadId": "thread_xyz",
  "response": "At Microsoft Build 2025, Microsoft announced [Microsoft Build 2025 Keynote]...",
  "citations": [
    {
      "title": "Microsoft Build 2025 Keynote",
      "uri": "https://news.microsoft.com/build-2025"
    }
  ],
  "status": "Completed",
  "error": null
}
```

---

## 3. Code Interpreter

Gives the agent the ability to **write and execute Python** in a sandboxed environment —
useful for data analysis, chart generation, and math.

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a data analyst. Use Python to compute answers and produce charts when helpful.
AGENT_NAME=code-agent

TOOL_1_TYPE=code_interpreter
```

**Test — data analysis**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Calculate the mean and standard deviation of: [4, 8, 15, 16, 23, 42]"}' | jq
```

**Test — follow-up computation (same thread)**
```bash
THREAD="thread_from_previous_response"

curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"Now draw a histogram of those values.\", \"threadId\": \"$THREAD\"}" | jq
```

> Code Interpreter can also work with uploaded files (attach via the Azure AI Foundry portal or Files API).

---

## 4. File Search

Enables **semantic search over uploaded documents** stored in a vector store.

**Prerequisites:** Create a vector store and upload documents via the Azure AI Foundry portal or SDK.

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a document assistant. Answer questions using only the provided documents.
AGENT_NAME=filesearch-agent

TOOL_1_TYPE=file_search
```

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What does our employee handbook say about remote work policy?"}' | jq
```

---

## 5. Azure AI Search

Connects the agent to an existing **Azure AI Search index** for grounded answers.

**Prerequisites:**
- An Azure AI Search resource with an index
- An Azure AI connection to that resource registered in Azure AI Foundry

### 5.1 Simple Query (default)

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a product support assistant. Answer based on the knowledge base.
AGENT_NAME=search-agent

TOOL_1_TYPE=azure_ai_search
TOOL_1_CONNECTION=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.MachineLearningServices/workspaces/<ws>/connections/<search-conn>
TOOL_1_INDEX_NAME=product-kb
TOOL_1_TOP_K=5
TOOL_1_QUERY_TYPE=simple
```

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "How do I reset my password?"}' | jq
```

### 5.2 Semantic Ranking

Improves relevance for natural-language questions (requires semantic ranking enabled in your index).

**.env** — change just the query type:
```env
TOOL_1_QUERY_TYPE=semantic
```

### 5.3 Vector Search

Uses embedding-based similarity search (requires a vector field in your index).

**.env**
```env
TOOL_1_QUERY_TYPE=vector
```

### 5.4 Hybrid Search

Combines keyword + vector search for best coverage.

**.env**
```env
TOOL_1_QUERY_TYPE=vector_simple_hybrid
# or:
TOOL_1_QUERY_TYPE=vector_semantic_hybrid
```

**With an OData filter** (e.g. limit results to a specific category):
```env
TOOL_1_FILTER=category eq 'hardware'
```

---

## 6. SharePoint Grounding

Lets the agent search and retrieve content from **SharePoint Online**.

**Prerequisites:** A SharePoint connection registered in Azure AI Foundry  
([SharePoint tool guide](https://learn.microsoft.com/azure/ai-services/agents/how-to/tools/sharepoint))

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a company knowledge assistant with access to our SharePoint intranet.
AGENT_NAME=sharepoint-agent

TOOL_1_TYPE=sharepoint
TOOL_1_CONNECTION=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.MachineLearningServices/workspaces/<ws>/connections/<sp-conn>
```

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Find the latest quarterly financial report on SharePoint."}' | jq
```

---

## 7. Microsoft Fabric Data Agent

Connects the agent to a **Microsoft Fabric** data skill for natural-language queries over structured data.

**Prerequisites:** A Fabric AI skill connection registered in Azure AI Foundry

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a business intelligence assistant. Query Fabric data to answer questions.
AGENT_NAME=fabric-agent

TOOL_1_TYPE=fabric
TOOL_1_CONNECTION=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.MachineLearningServices/workspaces/<ws>/connections/<fabric-conn>
```

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What were total sales by region for Q1 2025?"}' | jq
```

---

## 8. Bing Custom Search

Like Bing Grounding but scoped to a **specific domain or site list** you configure via Bing Custom Search.

**Prerequisites:**
- A Bing Custom Search instance at [customsearch.ai](https://www.customsearch.ai/)
- A Bing Custom Search connection in Azure AI Foundry

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a product assistant that searches only the official documentation site.
AGENT_NAME=custom-search-agent

TOOL_1_TYPE=bing_custom_search
TOOL_1_CONNECTION=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<acct>/connections/<bing-custom-conn>
TOOL_1_INSTANCE_NAME=my-docs-search-instance
```

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "How do I configure TLS in our product?"}' | jq
```

---

## 9. Browser Automation

Lets the agent **navigate and interact with web pages** using browser automation.

**Prerequisites:** A Browser Automation connection registered in Azure AI Foundry

**.env**
```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a web automation assistant. Complete web tasks as instructed.
AGENT_NAME=browser-agent

TOOL_1_TYPE=browser_automation
TOOL_1_CONNECTION=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.MachineLearningServices/workspaces/<ws>/connections/<browser-conn>
```

**Test**
```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Go to https://example.com and tell me the page title."}' | jq
```

---

## 10. Combining Multiple Tools

Tools are numbered from `1` upward and stacked freely. Here are two practical multi-tool recipes.

### Recipe A: Research agent (Bing + Code Interpreter)

```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are a research analyst. Search the web for data, then use Python to analyse and visualise it.
AGENT_NAME=research-agent

TOOL_1_TYPE=bing_grounding
TOOL_1_CONNECTION=<bing-connection-id>

TOOL_2_TYPE=code_interpreter
```

```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Find the population data for the top 5 EU countries and plot a bar chart."}' | jq
```

---

### Recipe B: Enterprise assistant (SharePoint + MCP ticketing + Azure AI Search)

```env
PROJECT_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
MODEL_DEPLOYMENT_NAME=gpt-4o
SYSTEM_PROMPT=You are an enterprise assistant. You can search documentation, look up tickets, and query the knowledge base.
AGENT_NAME=enterprise-agent

# Internal documentation (SharePoint)
TOOL_1_TYPE=sharepoint
TOOL_1_CONNECTION=<sp-connection-id>

# IT ticketing system (MCP)
TOOL_2_TYPE=mcp
TOOL_2_NAME=ticketing
TOOL_2_URL=https://itsm-mcp.internal.example.com/sse
TOOL_2_ALLOWED_TOOLS=search_tickets,get_ticket,create_ticket
TOOL_2_HEADERS=Authorization=Bearer <token>
TOOL_2_REQUIRE_APPROVAL=never

# Product knowledge base (Azure AI Search)
TOOL_3_TYPE=azure_ai_search
TOOL_3_CONNECTION=<search-connection-id>
TOOL_3_INDEX_NAME=product-kb
TOOL_3_TOP_K=5
TOOL_3_QUERY_TYPE=semantic
```

```bash
curl -s -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "A customer reports that the export feature is broken. Find relevant KB articles and create a P2 support ticket."}' | jq
```

---

## 11. Environment Variable Quick-Reference

### Core (all scenarios)

| Variable | Required | Example |
|---|---|---|
| `PROJECT_ENDPOINT` | ✅ | `https://<resource>.services.ai.azure.com/api/projects/<project>` |
| `MODEL_DEPLOYMENT_NAME` | ✅ | `gpt-4o` |
| `SYSTEM_PROMPT` | | `You are a helpful assistant.` |
| `AGENT_NAME` | | `my-agent` |
| `AGENT_TEMPERATURE` | | `0.7` |
| `AGENT_MAX_TOKENS` | | `2048` |
| `AGENT_REUSE_EXISTING` | | `true` |
| `ENABLE_SWAGGER` | | `true` |

### Authentication (DefaultAzureCredential)

| Variable | Description |
|---|---|
| `AZURE_TENANT_ID` | Service-principal tenant ID |
| `AZURE_CLIENT_ID` | Service-principal / managed-identity client ID |
| `AZURE_CLIENT_SECRET` | Service-principal client secret |

### Tool variables

| TOOL_N_TYPE | Required variables | Optional variables |
|---|---|---|
| `bing_grounding` | `TOOL_N_CONNECTION` | |
| `code_interpreter` | _(none)_ | |
| `file_search` | _(none)_ | |
| `azure_ai_search` | `TOOL_N_CONNECTION`, `TOOL_N_INDEX_NAME` | `TOOL_N_TOP_K`, `TOOL_N_QUERY_TYPE`, `TOOL_N_FILTER` |
| `mcp` | `TOOL_N_NAME`, `TOOL_N_URL` | `TOOL_N_ALLOWED_TOOLS`, `TOOL_N_HEADERS`, `TOOL_N_REQUIRE_APPROVAL` |
| `bing_custom_search` | `TOOL_N_CONNECTION`, `TOOL_N_INSTANCE_NAME` | |
| `sharepoint` | `TOOL_N_CONNECTION` | |
| `fabric` | `TOOL_N_CONNECTION` | |
| `browser_automation` | `TOOL_N_CONNECTION` | |

### MCP-specific variable details

| Variable | Format | Default | Example |
|---|---|---|---|
| `TOOL_N_NAME` | Any string (no spaces recommended) | — | `ticketing_server` |
| `TOOL_N_URL` | HTTPS SSE endpoint URL | — | `https://mcp.example.com/sse` |
| `TOOL_N_ALLOWED_TOOLS` | Comma-separated tool names | _(all tools allowed)_ | `search,get_record` |
| `TOOL_N_HEADERS` | `Key=Value` pairs separated by `\|` | _(no headers)_ | `Authorization=Bearer abc\|X-Tenant=acme` |
| `TOOL_N_REQUIRE_APPROVAL` | `never` or `always` | `never` | `never` |

### Azure AI Search query types

| Value | Description |
|---|---|
| `simple` | Keyword search (default) |
| `semantic` | Semantic re-ranking (requires semantic ranking in your index) |
| `vector` | Pure embedding-based similarity search |
| `vector_simple_hybrid` | Vector + keyword |
| `vector_semantic_hybrid` | Vector + keyword + semantic re-ranking |

---

## Verifying Agent Tool Configuration

After starting the container, inspect which tools are active:

```bash
curl -s http://localhost:8080/api/agent | jq
```

Expected output:
```json
{
  "agentId": "asst_abc123",
  "agentName": "my-agent",
  "model": "gpt-4o",
  "systemPrompt": "You are a helpful assistant.",
  "tools": [
    "MCPToolDefinition",
    "BingGroundingToolDefinition"
  ]
}
```

---

## Health Check

```bash
curl -s http://localhost:8080/api/health | jq
# { "status": "healthy" }
```

---

## Interactive Swagger UI

Set `ENABLE_SWAGGER=true` in your `.env` and open:

```
http://localhost:8080/swagger
```

You can send chat requests, inspect schemas, and try all endpoints directly from the browser.
