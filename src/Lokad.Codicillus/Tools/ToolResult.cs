using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Tools;

public abstract record ToolResult
{
    public string CallId { get; init; } = string.Empty;
}

public sealed record FunctionToolResult : ToolResult
{
    public FunctionCallOutputPayload Output { get; init; } = new();
}

public sealed record CustomToolResult : ToolResult
{
    public string Output { get; init; } = string.Empty;
}

public static class ToolResultExtensions
{
    public static ResponseInputItem ToResponseInputItem(this ToolResult result) =>
        result switch
        {
            FunctionToolResult functionResult => new ResponseInputFunctionCallOutputItem(
                functionResult.CallId,
                functionResult.Output),
            CustomToolResult customResult => new ResponseInputCustomToolCallOutputItem(
                customResult.CallId,
                customResult.Output),
            _ => throw new InvalidOperationException("Unknown tool result.")
        };
}
