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
builder.Services.AddHttpClient<AgentConnector>();
builder.Services.AddSingleton<TaskProcessor>();
builder.Services.AddSingleton<OrchestratorService>();

// Enable Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Agent Orchestrator API",
        Version = "v1",
        Description = "Plug-and-play agent orchestration framework. " +
                      "Link agents together via environment variables to create processing pipelines.",
    });
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agent Orchestrator API v1"));

app.MapTaskEndpoints();

// ── Startup info ─────────────────────────────────────────────────────────────
Console.WriteLine($"[AgentOrchestrator] Agent '{options.AgentName}' (ID: {options.AgentId}) starting...");
Console.WriteLine($"[AgentOrchestrator] Downstream: {options.DownstreamAgentUrl ?? "(none – terminal agent)"}");
Console.WriteLine($"[AgentOrchestrator] Auto-handoff: {options.AutoHandoff}");

app.Run();
return 0;
