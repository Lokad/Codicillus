namespace Lokad.Codicillus.Abstractions;

public sealed record ModelCapabilities
{
    public bool SupportsReasoningSummaries { get; init; }
    public bool SupportsParallelToolCalls { get; init; }
    public bool SupportsPromptCacheKey { get; init; }
    public bool SupportsRemoteCompaction { get; init; }
    public bool SupportsOutputSchema { get; init; }
}
