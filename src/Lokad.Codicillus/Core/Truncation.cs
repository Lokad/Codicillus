using System.Text;
using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Core;

public static class Truncation
{
    private const int ApproxBytesPerToken = 4;

    public static string FormattedTruncateText(string content, TruncationPolicy policy)
    {
        if (content.Length <= policy.ByteBudget)
        {
            return content;
        }
        var totalLines = content.Split('\n').Length;
        var result = TruncateText(content, policy);
        return $"Total output lines: {totalLines}\n\n{result}";
    }

    public static string TruncateText(string content, TruncationPolicy policy)
    {
        return policy.Kind == TruncationPolicyKind.Bytes
            ? TruncateWithByteEstimate(content, policy)
            : TruncateWithTokenBudget(content, policy).Truncated;
    }

    public static IReadOnlyList<FunctionCallOutputContentItem> TruncateFunctionOutputItemsWithPolicy(
        IReadOnlyList<FunctionCallOutputContentItem> items,
        TruncationPolicy policy)
    {
        var output = new List<FunctionCallOutputContentItem>(items.Count);
        var remainingBudget = policy.Kind == TruncationPolicyKind.Bytes
            ? policy.ByteBudget
            : policy.TokenBudget;
        var omittedTextItems = 0;

        foreach (var item in items)
        {
            if (item is FunctionCallOutputText textItem)
            {
                if (remainingBudget == 0)
                {
                    omittedTextItems++;
                    continue;
                }

                var cost = policy.Kind == TruncationPolicyKind.Bytes
                    ? textItem.Text.Length
                    : ApproxTokenCount(textItem.Text);

                if (cost <= remainingBudget)
                {
                    output.Add(textItem);
                    remainingBudget -= cost;
                }
                else
                {
                    var snippetPolicy = policy.Kind == TruncationPolicyKind.Bytes
                        ? TruncationPolicy.Bytes(remainingBudget)
                        : TruncationPolicy.Tokens(remainingBudget);
                    var snippet = TruncateText(textItem.Text, snippetPolicy);
                    if (string.IsNullOrEmpty(snippet))
                    {
                        omittedTextItems++;
                    }
                    else
                    {
                        output.Add(new FunctionCallOutputText(snippet));
                    }
                    remainingBudget = 0;
                }
            }
            else
            {
                output.Add(item);
            }
        }

        if (omittedTextItems > 0)
        {
            output.Add(new FunctionCallOutputText($"[omitted {omittedTextItems} text items ...]"));
        }

        return output;
    }

    public static int ApproxTokenCount(string text)
    {
        return (text.Length + ApproxBytesPerToken - 1) / ApproxBytesPerToken;
    }

    public static int ApproxBytesForTokens(int tokens) => tokens * ApproxBytesPerToken;

    public static int ApproxTokensFromByteCount(int bytes)
    {
        return (bytes + ApproxBytesPerToken - 1) / ApproxBytesPerToken;
    }

    private static (string Truncated, long? OriginalTokenCount) TruncateWithTokenBudget(
        string text,
        TruncationPolicy policy)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (string.Empty, null);
        }

        var maxTokens = policy.TokenBudget;
        if (maxTokens > 0 && text.Length <= ApproxBytesForTokens(maxTokens))
        {
            return (text, null);
        }

        var truncated = TruncateWithByteEstimate(text, policy);
        if (truncated == text)
        {
            return (truncated, null);
        }

        return (truncated, ApproxTokenCount(text));
    }

    private static string TruncateWithByteEstimate(string text, TruncationPolicy policy)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var maxBytes = policy.ByteBudget;
        if (maxBytes == 0)
        {
            var marker = FormatTruncationMarker(policy, RemovedUnitsForSource(policy, text.Length, text.Length));
            return marker;
        }

        if (text.Length <= maxBytes)
        {
            return text;
        }

        var (leftBudget, rightBudget) = SplitBudget(maxBytes);
        var (removedChars, left, right) = SplitString(text, leftBudget, rightBudget);
        var marker = FormatTruncationMarker(policy, RemovedUnitsForSource(policy, text.Length - maxBytes, removedChars));
        return AssembleTruncatedOutput(left, right, marker);
    }

    private static (int RemovedChars, string Left, string Right) SplitString(
        string text,
        int beginningBytes,
        int endBytes)
    {
        if (text.Length == 0)
        {
            return (0, string.Empty, string.Empty);
        }

        var prefixEnd = 0;
        var suffixStart = text.Length;
        var removedChars = 0;
        var suffixStarted = false;
        var tailStartTarget = Math.Max(0, text.Length - endBytes);

        for (var i = 0; i < text.Length; i++)
        {
            if (i + 1 <= beginningBytes)
            {
                prefixEnd = i + 1;
                continue;
            }

            if (i >= tailStartTarget)
            {
                if (!suffixStarted)
                {
                    suffixStart = i;
                    suffixStarted = true;
                }
                continue;
            }

            removedChars++;
        }

        if (suffixStart < prefixEnd)
        {
            suffixStart = prefixEnd;
        }

        return (removedChars, text[..prefixEnd], text[suffixStart..]);
    }

    private static string FormatTruncationMarker(TruncationPolicy policy, long removedCount)
    {
        return policy.Kind == TruncationPolicyKind.Tokens
            ? $"…{removedCount} tokens truncated…"
            : $"…{removedCount} chars truncated…";
    }

    private static (int Left, int Right) SplitBudget(int budget)
    {
        var left = budget / 2;
        return (left, budget - left);
    }

    private static long RemovedUnitsForSource(TruncationPolicy policy, int removedBytes, int removedChars)
    {
        return policy.Kind == TruncationPolicyKind.Tokens
            ? ApproxTokensFromByteCount(removedBytes)
            : removedChars;
    }

    private static string AssembleTruncatedOutput(string prefix, string suffix, string marker)
    {
        var sb = new StringBuilder(prefix.Length + suffix.Length + marker.Length + 1);
        sb.Append(prefix);
        sb.Append(marker);
        sb.Append(suffix);
        return sb.ToString();
    }
}
