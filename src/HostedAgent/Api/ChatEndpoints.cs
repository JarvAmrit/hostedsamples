using HostedAgent.Models;
using HostedAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace HostedAgent.Api;

/// <summary>
/// Registers all chat-related HTTP endpoints as ASP.NET Core minimal-API routes.
/// </summary>
public static class ChatEndpoints
{
    public static WebApplication MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        // GET /api/health
        group.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
             .WithName("GetHealth")
             .WithSummary("Returns a simple health-check response.");

        // GET /api/agent
        group.MapGet("/agent", async (AgentService agentService, CancellationToken ct) =>
        {
            var info = await agentService.GetAgentInfoAsync(ct);
            return Results.Ok(info);
        })
        .WithName("GetAgentInfo")
        .WithSummary("Returns metadata about the active agent.");

        // POST /api/chat
        group.MapPost("/chat", async (
            [FromBody] ChatRequest request,
            AgentService agentService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { error = "Message must not be empty." });

            var response = await agentService.ChatAsync(request, ct);
            return Results.Ok(response);
        })
        .WithName("Chat")
        .WithSummary("Send a message and receive a reply from the agent.")
        .WithDescription(
            "Pass an optional threadId to continue an existing conversation. " +
            "The response always includes the threadId to use for follow-up turns.");

        return app;
    }
}
