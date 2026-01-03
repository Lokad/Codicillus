using Lokad.Codicillus.Core;
using Lokad.Codicillus.Prompts;

namespace Lokad.Codicillus.Models;

public enum ApplyPatchToolType
{
    Function,
    Freeform
}

public enum ShellToolType
{
    Disabled,
    Default,
    Local,
    ShellCommand,
    UnifiedExec
}

public sealed record ModelFamily
{
    public string Slug { get; init; } = string.Empty;
    public string Family { get; init; } = string.Empty;
    public bool NeedsSpecialApplyPatchInstructions { get; init; }
    public long? ContextWindow { get; init; }
    public long? AutoCompactTokenLimit { get; init; }
    public bool SupportsReasoningSummaries { get; init; }
    public bool SupportsParallelToolCalls { get; init; }
    public ApplyPatchToolType? ApplyPatchToolType { get; init; }
    public string BaseInstructions { get; init; } = PromptCatalog.Load(PromptCatalog.BaseInstructions);
    public IReadOnlyList<string> ExperimentalSupportedTools { get; init; } = Array.Empty<string>();
    public long EffectiveContextWindowPercent { get; init; } = 95;
    public ShellToolType ShellType { get; init; } = ShellToolType.Default;
    public TruncationPolicy TruncationPolicy { get; init; } = TruncationPolicy.Bytes(10_000);

    public long? EffectiveContextWindow =>
        ContextWindow is null ? null : ContextWindow * EffectiveContextWindowPercent / 100;

    public long? AutoCompactLimit =>
        AutoCompactTokenLimit ?? (ContextWindow is null ? null : (ContextWindow * 9) / 10);
}
