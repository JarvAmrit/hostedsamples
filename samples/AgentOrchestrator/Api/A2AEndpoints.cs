using AgentOrchestrator.Configuration;
using AgentOrchestrator.Models;
using AgentOrchestrator.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentOrchestrator.Api;

/// <summary>
/// Registers A2A protocol endpoints:
///   POST /a2a                     — JSON-RPC 2.0 entry point (message/send, tasks/get, tasks/cancel)
///   GET  /.well-known/agent.json  — AgentCard discovery
///   GET  /api/health              — Health check
/// </summary>
public static class A2AEndpoints
{
    public static WebApplication MapA2AEndpoints(this WebApplication app)
    {
        // ── A2A JSON-RPC 2.0 endpoint ─────────────────────────────────────────
        app.MapPost("/a2a", async (
            [FromBody] JsonRpcRequest request,
            A2AHandler handler,
            CancellationToken ct) =>
        {
            var response = await handler.HandleAsync(request, ct);
            return Results.Json(response);
        })
        .WithName("A2A")
        .WithSummary("A2A Protocol JSON-RPC 2.0 endpoint")
        .WithDescription(
            "Handles all A2A protocol methods: message/send, tasks/get, tasks/cancel. " +
            "Send JSON-RPC 2.0 requests per the A2A specification.");

        // ── AgentCard discovery ───────────────────────────────────────────────
        app.MapGet("/.well-known/agent.json", (OrchestratorOptions options, HttpContext httpContext) =>
        {
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

            var card = new AgentCard
            {
                Name = options.AgentName,
                Description = options.AgentDescription,
                Url = $"{baseUrl}/a2a",
                Version = "1.0.0",
                Capabilities = new AgentCapabilities
                {
                    Streaming = false,
                    PushNotifications = false,
                    StateTransitionHistory = true,
                },
                Skills = options.Skills.Count > 0
                    ? options.Skills.Select(s => new AgentSkill
                    {
                        Id = s.Key,
                        Name = s.Key,
                        Description = s.Value,
                    }).ToList()
                    : [new AgentSkill
                    {
                        Id = "general",
                        Name = "General Processing",
                        Description = "Processes any text input and optionally hands off to downstream agents.",
                    }],
                DefaultInputModes = ["text", "data"],
                DefaultOutputModes = ["text", "data"],
                Provider = new AgentProvider
                {
                    Organization = options.Organization,
                },
            };

            return Results.Json(card);
        })
        .WithName("AgentCard")
        .WithSummary("A2A AgentCard discovery endpoint")
        .WithDescription("Returns this agent's capabilities, skills, and connection information per the A2A spec.");

        // ── Health check ──────────────────────────────────────────────────────
        app.MapGet("/api/health", (OrchestratorOptions options) =>
            Results.Ok(new
            {
                status = "healthy",
                agentId = options.AgentId,
                agentName = options.AgentName,
                protocol = "a2a",
                hasDownstream = !string.IsNullOrWhiteSpace(options.DownstreamAgentUrl),
            }))
            .WithName("GetHealth")
            .WithSummary("Health check with agent info.");

        return app;
    }
}
