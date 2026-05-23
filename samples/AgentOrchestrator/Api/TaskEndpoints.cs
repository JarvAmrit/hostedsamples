using AgentOrchestrator.Configuration;
using AgentOrchestrator.Models;
using AgentOrchestrator.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Api;

/// <summary>
/// Registers all orchestration-related HTTP endpoints.
/// </summary>
public static class TaskEndpoints
{
    public static WebApplication MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        // GET /api/health
        group.MapGet("/health", (OrchestratorOptions options) =>
            Results.Ok(new
            {
                status = "healthy",
                agentId = options.AgentId,
                agentName = options.AgentName,
                hasDownstream = !string.IsNullOrWhiteSpace(options.DownstreamAgentUrl),
            }))
            .WithName("GetHealth")
            .WithSummary("Health check with agent info.");

        // POST /api/tasks/submit – Submit a new task for orchestration
        group.MapPost("/tasks/submit", async (
            [FromBody] TaskSubmitRequest request,
            OrchestratorService orchestrator,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Input))
                return Results.BadRequest(new { error = "Input must not be empty." });

            var task = new TaskPayload
            {
                Input = request.Input,
                Metadata = request.Metadata ?? [],
            };

            var result = await orchestrator.ExecuteAsync(task, request.CallbackUrl, ct);
            return Results.Ok(result);
        })
        .WithName("SubmitTask")
        .WithSummary("Submit a new task for orchestration.")
        .WithDescription(
            "The task will be processed locally, then automatically handed off " +
            "to the downstream agent if AUTO_HANDOFF is true and DOWNSTREAM_AGENT_URL is set.");

        // POST /api/tasks/receive – Receive a handoff from an upstream agent
        group.MapPost("/tasks/receive", async (
            [FromBody] HandoffRequest request,
            OrchestratorService orchestrator,
            OrchestratorOptions options,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            // Validate inbound API key if configured
            if (!string.IsNullOrWhiteSpace(options.InboundApiKey))
            {
                var providedKey = httpContext.Request.Headers["X-Agent-Key"].FirstOrDefault();
                if (providedKey != options.InboundApiKey)
                    return Results.Unauthorized();
            }

            var result = await orchestrator.ExecuteAsync(request.Task, request.CallbackUrl, ct);
            return Results.Ok(result);
        })
        .WithName("ReceiveTask")
        .WithSummary("Receive a task handoff from an upstream agent.")
        .WithDescription(
            "This endpoint is called by upstream agents to hand off tasks. " +
            "If INBOUND_API_KEY is set, the request must include a matching X-Agent-Key header.");

        // POST /api/tasks/handoff – Explicitly hand off without local processing
        group.MapPost("/tasks/handoff", async (
            [FromBody] TaskPayload task,
            OrchestratorService orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.HandoffAsync(task, ct: ct);
            return Results.Ok(result);
        })
        .WithName("HandoffTask")
        .WithSummary("Explicitly hand off a task to the downstream agent without local processing.");

        return app;
    }
}

/// <summary>Request body for submitting a new task.</summary>
public sealed class TaskSubmitRequest
{
    /// <summary>The task input/instruction.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>Optional metadata key-value pairs.</summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>Optional callback URL to receive the final result.</summary>
    public string? CallbackUrl { get; set; }
}
