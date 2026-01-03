using System.Text.Json;
using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Core;

public sealed record ModelPrompt
{
    public string Instructions { get; init; } = string.Empty;
    public IReadOnlyList<ResponseItem> Input { get; init; } = Array.Empty<ResponseItem>();
    public IReadOnlyList<ToolSpec> Tools { get; init; } = Array.Empty<ToolSpec>();
    public bool ParallelToolCalls { get; init; }
    public JsonElement? OutputSchema { get; init; }
    public string? PromptCacheKey { get; init; }
}
