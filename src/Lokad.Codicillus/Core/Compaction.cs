using Lokad.Codicillus.Prompts;
using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Core;

public static class Compaction
{
    public const int CompactUserMessageMaxTokens = 20_000;

    public static string SummarizationPrompt => PromptCatalog.Load(PromptCatalog.CompactPrompt);

    public static string SummaryPrefix => PromptCatalog.Load(PromptCatalog.CompactSummaryPrefix);

    public static IReadOnlyList<string> CollectUserMessages(IEnumerable<ResponseItem> items)
    {
        var messages = new List<string>();
        foreach (var item in items)
        {
            if (item is not MessageResponseItem message || message.Role != "user")
            {
                continue;
            }

            var text = ContentItemsToText(message.Content);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (IsSummaryMessage(text))
            {
                continue;
            }

            messages.Add(text);
        }

        return messages;
    }

    public static IReadOnlyList<ResponseItem> BuildCompactedHistory(
        IReadOnlyList<ResponseItem> initialContext,
        IReadOnlyList<string> userMessages,
        string summaryText,
        int maxTokens = CompactUserMessageMaxTokens)
    {
        var history = new List<ResponseItem>(initialContext);
        var selected = new List<string>();

        var remaining = maxTokens;
        for (var i = userMessages.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var message = userMessages[i];
            var tokens = Truncation.ApproxTokenCount(message);
            if (tokens <= remaining)
            {
                selected.Add(message);
                remaining -= tokens;
            }
            else
            {
                var truncated = Truncation.TruncateText(message, TruncationPolicy.Tokens(remaining));
                selected.Add(truncated);
                break;
            }
        }

        selected.Reverse();
        foreach (var message in selected)
        {
            history.Add(new MessageResponseItem("user", [new InputTextContent(message)], null));
        }

        var summary = string.IsNullOrWhiteSpace(summaryText) ? "(no summary available)" : summaryText;
        history.Add(new MessageResponseItem("user", [new InputTextContent(summary)], null));
        return history;
    }

    public static string? ContentItemsToText(IReadOnlyList<ContentItem> items)
    {
        var pieces = new List<string>();
        foreach (var item in items)
        {
            switch (item)
            {
                case InputTextContent input when !string.IsNullOrWhiteSpace(input.Text):
                    pieces.Add(input.Text);
                    break;
                case OutputTextContent output when !string.IsNullOrWhiteSpace(output.Text):
                    pieces.Add(output.Text);
                    break;
            }
        }

        return pieces.Count == 0 ? null : string.Join("\n", pieces);
    }

    public static bool IsSummaryMessage(string message)
    {
        return message.StartsWith($"{SummaryPrefix}\n", StringComparison.Ordinal);
    }
}
