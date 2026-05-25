using AgentOrchestrator.Api;
using AgentOrchestrator.Configuration;
using AgentOrchestrator.Services;

// ── Load & validate configuration from environment variables ─────────────────
OrchestratorOptions options;
try
{
    options = OrchestratorOptions.FromEnvironment();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"[AgentOrchestrator] Configuration error: {ex.Message}");
    return 1;
}

// ── Build the ASP.NET Core host ───────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register services
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<TaskStore>();
builder.Services.AddHttpClient<A2AClient>();
builder.Services.AddSingleton<A2AHandler>();

// Enable Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "A2A Agent",
        Version = "v1",
        Description = "Agent-to-Agent (A2A) protocol compliant agent. " +
                      "Receives and sends tasks via JSON-RPC 2.0 over HTTP. " +
                      "Supports message/send, tasks/get, tasks/cancel, and AgentCard discovery.",
    });
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "A2A Agent v1"));

app.MapA2AEndpoints();

// ── Startup info ─────────────────────────────────────────────────────────────
Console.WriteLine($"[A2A Agent] '{options.AgentName}' (ID: {options.AgentId}) starting...");
Console.WriteLine($"[A2A Agent] Protocol: A2A (JSON-RPC 2.0)");
Console.WriteLine($"[A2A Agent] Endpoints: POST /a2a, GET /.well-known/agent.json");
Console.WriteLine($"[A2A Agent] Downstream: {options.DownstreamAgentUrl ?? "(none – terminal agent)"}");
Console.WriteLine($"[A2A Agent] Auto-handoff: {options.AutoHandoff}");

app.Run();
return 0;
