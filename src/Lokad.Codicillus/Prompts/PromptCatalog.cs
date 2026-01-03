using System.Reflection;

namespace Lokad.Codicillus.Prompts;

public static class PromptCatalog
{
    public const string BaseInstructions = "Prompts.prompt.md";
    public const string Gpt5CodexInstructions = "Prompts.gpt_5_codex_prompt.md";
    public const string Gpt51Instructions = "Prompts.gpt_5_1_prompt.md";
    public const string Gpt52Instructions = "Prompts.gpt_5_2_prompt.md";
    public const string Gpt51CodexMaxInstructions = "Prompts.gpt-5.1-codex-max_prompt.md";
    public const string Gpt52CodexInstructions = "Prompts.gpt-5.2-codex_prompt.md";
    public const string CompactPrompt = "Prompts.Compact.prompt.md";
    public const string CompactSummaryPrefix = "Prompts.Compact.summary_prefix.md";

    public static string Load(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fullName = $"Lokad.Codicillus.{resourceName}";
        using var stream = assembly.GetManifestResourceStream(fullName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Missing embedded prompt: {fullName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
