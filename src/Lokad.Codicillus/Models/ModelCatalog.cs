using Lokad.Codicillus.Core;
using Lokad.Codicillus.Prompts;

namespace Lokad.Codicillus.Models;

public static class ModelCatalog
{
    public const long ContextWindow272K = 272_000;

    public static ModelFamily FindFamilyForModel(string slug)
    {
        if (slug.StartsWith("o3", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "o3") with
            {
                SupportsReasoningSummaries = true,
                NeedsSpecialApplyPatchInstructions = true,
                ContextWindow = 200_000
            };
        }
        if (slug.StartsWith("o4-mini", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "o4-mini") with
            {
                SupportsReasoningSummaries = true,
                NeedsSpecialApplyPatchInstructions = true,
                ContextWindow = 200_000
            };
        }
        if (slug.StartsWith("codex-mini-latest", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "codex-mini-latest") with
            {
                SupportsReasoningSummaries = true,
                NeedsSpecialApplyPatchInstructions = true,
                ShellType = ShellToolType.Local,
                ContextWindow = 200_000
            };
        }
        if (slug.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "gpt-4.1") with
            {
                NeedsSpecialApplyPatchInstructions = true,
                ContextWindow = 1_047_576
            };
        }
        if (slug.StartsWith("gpt-oss", StringComparison.OrdinalIgnoreCase) ||
            slug.StartsWith("openai/gpt-oss", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "gpt-oss") with
            {
                ApplyPatchToolType = Models.ApplyPatchToolType.Function,
                ContextWindow = 96_000
            };
        }
        if (slug.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "gpt-4o") with
            {
                NeedsSpecialApplyPatchInstructions = true,
                ContextWindow = 128_000
            };
        }
        if (slug.StartsWith("gpt-3.5", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "gpt-3.5") with
            {
                NeedsSpecialApplyPatchInstructions = true,
                ContextWindow = 16_385
            };
        }
        if (slug.StartsWith("gpt-5.2-codex", StringComparison.OrdinalIgnoreCase))
        {
            return CodexFamily(slug, PromptCatalog.Gpt52CodexInstructions);
        }
        if (slug.StartsWith("gpt-5.1-codex-max", StringComparison.OrdinalIgnoreCase))
        {
            return CodexFamily(slug, PromptCatalog.Gpt51CodexMaxInstructions) with
            {
                SupportsParallelToolCalls = false
            };
        }
        if (slug.StartsWith("gpt-5-codex", StringComparison.OrdinalIgnoreCase) ||
            slug.StartsWith("gpt-5.1-codex", StringComparison.OrdinalIgnoreCase) ||
            slug.StartsWith("codex-", StringComparison.OrdinalIgnoreCase))
        {
            return CodexFamily(slug, PromptCatalog.Gpt5CodexInstructions) with
            {
                SupportsParallelToolCalls = false
            };
        }
        if (slug.StartsWith("gpt-5.2", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, slug) with
            {
                SupportsReasoningSummaries = true,
                ApplyPatchToolType = Models.ApplyPatchToolType.Freeform,
                BaseInstructions = PromptCatalog.Load(PromptCatalog.Gpt52Instructions),
                ShellType = ShellToolType.ShellCommand,
                SupportsParallelToolCalls = true,
                ContextWindow = ContextWindow272K,
                TruncationPolicy = TruncationPolicy.Bytes(10_000)
            };
        }
        if (slug.StartsWith("gpt-5.1", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "gpt-5.1") with
            {
                SupportsReasoningSummaries = true,
                ApplyPatchToolType = Models.ApplyPatchToolType.Freeform,
                BaseInstructions = PromptCatalog.Load(PromptCatalog.Gpt51Instructions),
                ShellType = ShellToolType.ShellCommand,
                SupportsParallelToolCalls = true,
                ContextWindow = ContextWindow272K,
                TruncationPolicy = TruncationPolicy.Bytes(10_000)
            };
        }
        if (slug.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            return Base(slug, "gpt-5") with
            {
                SupportsReasoningSummaries = true,
                NeedsSpecialApplyPatchInstructions = true,
                ContextWindow = ContextWindow272K
            };
        }

        return Base(slug, slug);
    }

    private static ModelFamily Base(string slug, string family) => new()
    {
        Slug = slug,
        Family = family,
        BaseInstructions = PromptCatalog.Load(PromptCatalog.BaseInstructions),
        ContextWindow = ContextWindow272K,
        TruncationPolicy = TruncationPolicy.Bytes(10_000)
    };

    private static ModelFamily CodexFamily(string slug, string promptResource) => new()
    {
        Slug = slug,
        Family = slug,
        SupportsReasoningSummaries = true,
        ApplyPatchToolType = Models.ApplyPatchToolType.Freeform,
        BaseInstructions = PromptCatalog.Load(promptResource),
        ShellType = ShellToolType.ShellCommand,
        SupportsParallelToolCalls = true,
        ContextWindow = ContextWindow272K,
        TruncationPolicy = TruncationPolicy.Tokens(10_000)
    };
}
