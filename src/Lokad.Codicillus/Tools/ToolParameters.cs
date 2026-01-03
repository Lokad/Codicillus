using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Tools;

public sealed record ShellToolCallParams
{
    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; init; } = Array.Empty<string>();

    [JsonPropertyName("workdir")]
    public string? Workdir { get; init; }

    [JsonPropertyName("timeout_ms")]
    public long? TimeoutMs { get; init; }

    [JsonPropertyName("sandbox_permissions")]
    public string? SandboxPermissions { get; init; }

    [JsonPropertyName("justification")]
    public string? Justification { get; init; }
}

public sealed record ShellCommandToolCallParams
{
    [JsonPropertyName("command")]
    public string Command { get; init; } = string.Empty;

    [JsonPropertyName("workdir")]
    public string? Workdir { get; init; }

    [JsonPropertyName("login")]
    public bool? Login { get; init; }

    [JsonPropertyName("timeout_ms")]
    public long? TimeoutMs { get; init; }

    [JsonPropertyName("sandbox_permissions")]
    public string? SandboxPermissions { get; init; }

    [JsonPropertyName("justification")]
    public string? Justification { get; init; }
}

public sealed record ApplyPatchToolArgs
{
    [JsonPropertyName("input")]
    public string Input { get; init; } = string.Empty;
}
