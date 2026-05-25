using System.Text.Json.Serialization;

namespace AgentOrchestrator.Models;

// ═══════════════════════════════════════════════════════════════════════════════
// A2A Protocol Models – Agent-to-Agent (A2A) open standard
// Based on: https://a2a-protocol.org/latest/specification/
// Transport: JSON-RPC 2.0 over HTTP(S)
// ═══════════════════════════════════════════════════════════════════════════════

// ── JSON-RPC 2.0 Envelope ─────────────────────────────────────────────────────

/// <summary>
/// JSON-RPC 2.0 request envelope for all A2A operations.
/// </summary>
public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonRpcParams? Params { get; set; }
}

/// <summary>
/// Parameters for a JSON-RPC A2A request.
/// </summary>
public sealed class JsonRpcParams
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("message")]
    public A2AMessage? Message { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 success response.
/// </summary>
public sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 error object.
/// </summary>
public sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

// ── A2A Core Types ────────────────────────────────────────────────────────────

/// <summary>
/// A2A Task represents a unit of delegated work between agents.
/// </summary>
public sealed class A2ATask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("status")]
    public A2ATaskStatus Status { get; set; } = new();

    [JsonPropertyName("messages")]
    public List<A2AMessage> Messages { get; set; } = [];

    [JsonPropertyName("artifacts")]
    public List<A2AArtifact>? Artifacts { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Status of an A2A task.
/// </summary>
public sealed class A2ATaskStatus
{
    [JsonPropertyName("state")]
    public string State { get; set; } = A2ATaskState.Submitted;

    [JsonPropertyName("message")]
    public A2AMessage? Message { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");
}

/// <summary>
/// Valid task states per A2A specification.
/// </summary>
public static class A2ATaskState
{
    public const string Submitted = "submitted";
    public const string Working = "working";
    public const string InputRequired = "input-required";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Canceled = "canceled";
}

/// <summary>
/// A2A Message exchanged between agents (user or agent role).
/// </summary>
public sealed class A2AMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("parts")]
    public List<A2APart> Parts { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// A part of a message — supports text, file, and data content types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextPart), "text")]
[JsonDerivedType(typeof(FilePart), "file")]
[JsonDerivedType(typeof(DataPart), "data")]
public abstract class A2APart
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>Text content part.</summary>
public sealed class TextPart : A2APart
{
    [JsonPropertyName("type")]
    public override string Type => "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>File content part (inline bytes or URI reference).</summary>
public sealed class FilePart : A2APart
{
    [JsonPropertyName("type")]
    public override string Type => "file";

    [JsonPropertyName("file")]
    public A2AFileContent File { get; set; } = new();
}

/// <summary>Structured data part (arbitrary JSON).</summary>
public sealed class DataPart : A2APart
{
    [JsonPropertyName("type")]
    public override string Type => "data";

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// File content — either inline bytes (base64) or a URI.
/// </summary>
public sealed class A2AFileContent
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("bytes")]
    public string? Bytes { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}

/// <summary>
/// Artifact produced by an agent as the result of a task.
/// </summary>
public sealed class A2AArtifact
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parts")]
    public List<A2APart> Parts { get; set; } = [];

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

// ── Agent Card ────────────────────────────────────────────────────────────────

/// <summary>
/// AgentCard published at /.well-known/agent.json — describes agent capabilities.
/// </summary>
public sealed class AgentCard
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("capabilities")]
    public AgentCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("skills")]
    public List<AgentSkill>? Skills { get; set; }

    [JsonPropertyName("defaultInputModes")]
    public List<string> DefaultInputModes { get; set; } = ["text"];

    [JsonPropertyName("defaultOutputModes")]
    public List<string> DefaultOutputModes { get; set; } = ["text"];

    [JsonPropertyName("provider")]
    public AgentProvider? Provider { get; set; }
}

/// <summary>
/// Capabilities this agent supports.
/// </summary>
public sealed class AgentCapabilities
{
    [JsonPropertyName("streaming")]
    public bool Streaming { get; set; }

    [JsonPropertyName("pushNotifications")]
    public bool PushNotifications { get; set; }

    [JsonPropertyName("stateTransitionHistory")]
    public bool StateTransitionHistory { get; set; } = true;
}

/// <summary>
/// A skill/capability the agent can perform.
/// </summary>
public sealed class AgentSkill
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("examples")]
    public List<string>? Examples { get; set; }
}

/// <summary>
/// Provider information for the agent.
/// </summary>
public sealed class AgentProvider
{
    [JsonPropertyName("organization")]
    public string? Organization { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

// ── Streaming Events (SSE) ───────────────────────────────────────────────────

/// <summary>
/// Event sent during message/stream for task status updates.
/// </summary>
public sealed class TaskStatusUpdateEvent
{
    [JsonPropertyName("type")]
    public string Type => "status";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public A2ATaskStatus Status { get; set; } = new();

    [JsonPropertyName("final")]
    public bool Final { get; set; }
}

/// <summary>
/// Event sent during message/stream for artifact updates.
/// </summary>
public sealed class TaskArtifactUpdateEvent
{
    [JsonPropertyName("type")]
    public string Type => "artifact";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("artifact")]
    public A2AArtifact Artifact { get; set; } = new();
}
