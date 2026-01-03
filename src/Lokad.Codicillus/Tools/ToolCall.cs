namespace Lokad.Codicillus.Tools;

public abstract record ToolCall
{
    public string CallId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed record FunctionToolCall : ToolCall
{
    public string ArgumentsJson { get; init; } = string.Empty;
}

public sealed record CustomToolCall : ToolCall
{
    public string Input { get; init; } = string.Empty;
}
