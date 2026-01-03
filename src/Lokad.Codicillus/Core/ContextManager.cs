using System.Text.Json;
using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Core;

public sealed class ContextManager
{
    private readonly List<ResponseItem> _items = [];
    private TokenUsageInfo? _tokenInfo;

    public TokenUsageInfo? TokenInfo => _tokenInfo;

    public void SetTokenInfo(TokenUsageInfo? info) => _tokenInfo = info;

    public void RecordItems(IEnumerable<ResponseItem> items, TruncationPolicy policy)
    {
        foreach (var item in items)
        {
            if (!IsApiMessage(item) && item is not GhostSnapshotResponseItem)
            {
                continue;
            }
            _items.Add(ProcessItem(item, policy));
        }
    }

    public IReadOnlyList<ResponseItem> GetHistory()
    {
        NormalizeHistory();
        return _items.ToList();
    }

    public IReadOnlyList<ResponseItem> GetHistoryForPrompt()
    {
        var history = GetHistory().ToList();
        history.RemoveAll(item => item is GhostSnapshotResponseItem);
        return history;
    }

    public void Replace(IReadOnlyList<ResponseItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
    }

    public void RemoveFirstItem()
    {
        if (_items.Count == 0)
        {
            return;
        }
        var removed = _items[0];
        _items.RemoveAt(0);
        RemoveCorrespondingFor(_items, removed);
    }

    public long? EstimateTokenCount()
    {
        if (_items.Count == 0)
        {
            return null;
        }

        long total = 0;
        foreach (var item in _items)
        {
            if (item is GhostSnapshotResponseItem)
            {
                continue;
            }
            var serialized = JsonSerializer.Serialize(item);
            total += Truncation.ApproxTokenCount(serialized);
        }
        return total;
    }

    private static ResponseItem ProcessItem(ResponseItem item, TruncationPolicy policy)
    {
        var withBudget = policy.Multiply(1.2);
        return item switch
        {
            FunctionCallOutputResponseItem functionOutput => functionOutput with
            {
                Output = new FunctionCallOutputPayload
                {
                    Content = Truncation.TruncateText(functionOutput.Output.Content, withBudget),
                    ContentItems = functionOutput.Output.ContentItems is null
                        ? null
                        : Truncation.TruncateFunctionOutputItemsWithPolicy(
                            functionOutput.Output.ContentItems,
                            withBudget),
                    Success = functionOutput.Output.Success
                }
            },
            CustomToolCallOutputResponseItem customOutput => customOutput with
            {
                Output = Truncation.TruncateText(customOutput.Output, withBudget)
            },
            _ => item
        };
    }

    private void NormalizeHistory()
    {
        EnsureCallOutputsPresent(_items);
        RemoveOrphanOutputs(_items);
    }

    private static bool IsApiMessage(ResponseItem item) =>
        item switch
        {
            MessageResponseItem message => !string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase),
            FunctionCallResponseItem => true,
            FunctionCallOutputResponseItem => true,
            CustomToolCallResponseItem => true,
            CustomToolCallOutputResponseItem => true,
            LocalShellCallResponseItem => true,
            ReasoningResponseItem => true,
            WebSearchCallResponseItem => true,
            CompactionResponseItem => true,
            _ => false
        };

    private static void EnsureCallOutputsPresent(List<ResponseItem> items)
    {
        var insertions = new List<(int Index, ResponseItem Item)>();
        for (var i = 0; i < items.Count; i++)
        {
            switch (items[i])
            {
                case FunctionCallResponseItem functionCall when !items.Any(item =>
                    item is FunctionCallOutputResponseItem output && output.CallId == functionCall.CallId):
                    insertions.Add((i, new FunctionCallOutputResponseItem(
                        functionCall.CallId,
                        new FunctionCallOutputPayload { Content = "aborted" })));
                    break;
                case CustomToolCallResponseItem customCall when !items.Any(item =>
                    item is CustomToolCallOutputResponseItem output && output.CallId == customCall.CallId):
                    insertions.Add((i, new CustomToolCallOutputResponseItem(customCall.CallId, "aborted")));
                    break;
                case LocalShellCallResponseItem shellCall when shellCall.CallId is not null &&
                    !items.Any(item => item is FunctionCallOutputResponseItem output && output.CallId == shellCall.CallId):
                    insertions.Add((i, new FunctionCallOutputResponseItem(
                        shellCall.CallId!,
                        new FunctionCallOutputPayload { Content = "aborted" })));
                    break;
            }
        }

        for (var i = insertions.Count - 1; i >= 0; i--)
        {
            var (index, item) = insertions[i];
            items.Insert(index + 1, item);
        }
    }

    private static void RemoveOrphanOutputs(List<ResponseItem> items)
    {
        var functionCalls = items.OfType<FunctionCallResponseItem>().Select(c => c.CallId).ToHashSet();
        var shellCalls = items.OfType<LocalShellCallResponseItem>()
            .Select(c => c.CallId)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToHashSet();
        var customCalls = items.OfType<CustomToolCallResponseItem>().Select(c => c.CallId).ToHashSet();

        items.RemoveAll(item => item switch
        {
            FunctionCallOutputResponseItem output => !(functionCalls.Contains(output.CallId) || shellCalls.Contains(output.CallId)),
            CustomToolCallOutputResponseItem output => !customCalls.Contains(output.CallId),
            _ => false
        });
    }

    private static void RemoveCorrespondingFor(List<ResponseItem> items, ResponseItem removed)
    {
        switch (removed)
        {
            case FunctionCallResponseItem functionCall:
                RemoveFirst(items, item => item is FunctionCallOutputResponseItem output && output.CallId == functionCall.CallId);
                break;
            case FunctionCallOutputResponseItem functionOutput:
                RemoveFirst(items, item =>
                    item is FunctionCallResponseItem call && call.CallId == functionOutput.CallId);
                RemoveFirst(items, item =>
                    item is LocalShellCallResponseItem call && call.CallId == functionOutput.CallId);
                break;
            case CustomToolCallResponseItem customCall:
                RemoveFirst(items, item =>
                    item is CustomToolCallOutputResponseItem output && output.CallId == customCall.CallId);
                break;
            case CustomToolCallOutputResponseItem customOutput:
                RemoveFirst(items, item =>
                    item is CustomToolCallResponseItem call && call.CallId == customOutput.CallId);
                break;
            case LocalShellCallResponseItem shellCall when shellCall.CallId is not null:
                RemoveFirst(items, item =>
                    item is FunctionCallOutputResponseItem output && output.CallId == shellCall.CallId);
                break;
        }
    }

    private static void RemoveFirst(List<ResponseItem> items, Predicate<ResponseItem> predicate)
    {
        var index = items.FindIndex(predicate);
        if (index >= 0)
        {
            items.RemoveAt(index);
        }
    }
}
