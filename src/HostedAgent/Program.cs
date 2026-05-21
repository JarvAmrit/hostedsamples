using HostedAgent.Configuration;
using HostedAgent.Api;
using HostedAgent.Services;

// ── Load & validate configuration from environment variables ─────────────────
AgentOptions agentOptions;
try
{
    agentOptions = AgentOptions.FromEnvironment();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"[HostedAgent] Configuration error: {ex.Message}");
    return 1;
}

// ── Build the ASP.NET Core host ───────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register services
builder.Services.AddSingleton(agentOptions);
builder.Services.AddSingleton<ToolResolver>();
builder.Services.AddSingleton<AgentService>();

// Enable Swagger/OpenAPI for easy interactive testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Hosted Agent API",
        Version     = "v1",
        Description = "Plug-and-play Azure AI Foundry hosted agent. " +
                      "Configure everything via environment variables.",
    });
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment() ||
    string.Equals(
        Environment.GetEnvironmentVariable("ENABLE_SWAGGER"), "true",
        StringComparison.OrdinalIgnoreCase))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hosted Agent API v1"));
}

app.MapChatEndpoints();

// ── Eager agent initialisation (optional warm-up) ────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var agentService = scope.ServiceProvider.GetRequiredService<AgentService>();
    try
    {
        var info = await agentService.GetAgentInfoAsync();
        Console.WriteLine(
            $"[HostedAgent] Agent ready: '{info.AgentName}' (ID: {info.AgentId}), " +
            $"model={info.Model}, tools=[{string.Join(", ", info.Tools)}]");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[HostedAgent] Warning: Agent pre-warm failed: {ex.Message}");
    }
}

app.Run();
return 0;
