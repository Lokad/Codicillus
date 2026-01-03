using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

public sealed record RateLimitSnapshot
{
    [JsonPropertyName("primary")]
    public RateLimitWindow? Primary { get; init; }

    [JsonPropertyName("secondary")]
    public RateLimitWindow? Secondary { get; init; }

    [JsonPropertyName("credits")]
    public CreditsSnapshot? Credits { get; init; }

    [JsonPropertyName("plan_type")]
    public string? PlanType { get; init; }
}

public sealed record RateLimitWindow
{
    [JsonPropertyName("used_percent")]
    public double UsedPercent { get; init; }

    [JsonPropertyName("window_minutes")]
    public long? WindowMinutes { get; init; }

    [JsonPropertyName("resets_at")]
    public long? ResetsAt { get; init; }
}

public sealed record CreditsSnapshot
{
    [JsonPropertyName("has_credits")]
    public bool HasCredits { get; init; }

    [JsonPropertyName("unlimited")]
    public bool Unlimited { get; init; }

    [JsonPropertyName("balance")]
    public string? Balance { get; init; }
}
