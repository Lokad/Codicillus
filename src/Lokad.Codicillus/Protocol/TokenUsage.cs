using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

public sealed record TokenUsage
{
    [JsonPropertyName("input_tokens")]
    public long InputTokens { get; init; }

    [JsonPropertyName("cached_input_tokens")]
    public long CachedInputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public long OutputTokens { get; init; }

    [JsonPropertyName("reasoning_output_tokens")]
    public long ReasoningOutputTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public long TotalTokens { get; init; }
}

public sealed record TokenUsageInfo
{
    [JsonPropertyName("total_token_usage")]
    public TokenUsage TotalTokenUsage { get; init; } = new();

    [JsonPropertyName("last_token_usage")]
    public TokenUsage LastTokenUsage { get; init; } = new();

    [JsonPropertyName("model_context_window")]
    public long? ModelContextWindow { get; init; }
}
