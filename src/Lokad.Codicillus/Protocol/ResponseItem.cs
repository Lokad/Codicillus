using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

[JsonConverter(typeof(ResponseItemConverter))]
public abstract record ResponseItem;

public sealed record MessageResponseItem(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] IReadOnlyList<ContentItem> Content,
    [property: JsonPropertyName("id")] string? Id = null
) : ResponseItem;

public sealed record ReasoningResponseItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("summary")] IReadOnlyList<ReasoningSummaryItem> Summary,
    [property: JsonPropertyName("content")] IReadOnlyList<ReasoningContentItem>? Content,
    [property: JsonPropertyName("encrypted_content")] string? EncryptedContent
) : ResponseItem;

public sealed record LocalShellCallResponseItem(
    [property: JsonPropertyName("call_id")] string? CallId,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] LocalShellStatus Status,
    [property: JsonPropertyName("action")] LocalShellAction Action
) : ResponseItem;

public sealed record FunctionCallResponseItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] string Arguments,
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("id")] string? Id = null
) : ResponseItem;

public sealed record FunctionCallOutputResponseItem(
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("output")] FunctionCallOutputPayload Output
) : ResponseItem;

public sealed record CustomToolCallResponseItem(
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("input")] string Input,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("id")] string? Id = null
) : ResponseItem;

public sealed record CustomToolCallOutputResponseItem(
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("output")] string Output
) : ResponseItem;

public sealed record WebSearchCallResponseItem(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("action")] WebSearchAction Action,
    [property: JsonPropertyName("id")] string? Id = null
) : ResponseItem;

public sealed record GhostSnapshotResponseItem(
    [property: JsonPropertyName("ghost_commit")] JsonElement GhostCommit
) : ResponseItem;

public sealed record CompactionResponseItem(
    [property: JsonPropertyName("encrypted_content")] string EncryptedContent
) : ResponseItem;

public sealed record OtherResponseItem(JsonElement Raw) : ResponseItem;

public sealed record ReasoningSummaryItem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text
)
{
    public static ReasoningSummaryItem SummaryText(string text) => new("summary_text", text);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ReasoningTextContentItem), "reasoning_text")]
[JsonDerivedType(typeof(ReasoningPlainTextContentItem), "text")]
public abstract record ReasoningContentItem;

public sealed record ReasoningTextContentItem(
    [property: JsonPropertyName("text")] string Text
) : ReasoningContentItem;

public sealed record ReasoningPlainTextContentItem(
    [property: JsonPropertyName("text")] string Text
) : ReasoningContentItem;

[JsonConverter(typeof(LocalShellStatusConverter))]
public enum LocalShellStatus
{
    Completed,
    InProgress,
    Incomplete
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LocalShellExecAction), "exec")]
public abstract record LocalShellAction;

public sealed record LocalShellExecAction(
    [property: JsonPropertyName("command")] IReadOnlyList<string> Command,
    [property: JsonPropertyName("timeout_ms")] long? TimeoutMs,
    [property: JsonPropertyName("working_directory")] string? WorkingDirectory
) : LocalShellAction;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WebSearchActionSearch), "search")]
[JsonDerivedType(typeof(WebSearchActionOpenPage), "open_page")]
[JsonDerivedType(typeof(WebSearchActionFindInPage), "find_in_page")]
public abstract record WebSearchAction;

public sealed record WebSearchActionSearch(
    [property: JsonPropertyName("query")] string? Query
) : WebSearchAction;

public sealed record WebSearchActionOpenPage(
    [property: JsonPropertyName("url")] string? Url
) : WebSearchAction;

public sealed record WebSearchActionFindInPage(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("pattern")] string? Pattern
) : WebSearchAction;

internal sealed class ResponseItemConverter : JsonConverter<ResponseItem>
{
    public override ResponseItem? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        if (!doc.RootElement.TryGetProperty("type", out var typeProperty))
        {
            return new OtherResponseItem(doc.RootElement.Clone());
        }

        var type = typeProperty.GetString();
        return type switch
        {
            "message" => Deserialize<MessageResponseItem>(doc, options),
            "reasoning" => Deserialize<ReasoningResponseItem>(doc, options),
            "local_shell_call" => Deserialize<LocalShellCallResponseItem>(doc, options),
            "function_call" => Deserialize<FunctionCallResponseItem>(doc, options),
            "function_call_output" => Deserialize<FunctionCallOutputResponseItem>(doc, options),
            "custom_tool_call" => Deserialize<CustomToolCallResponseItem>(doc, options),
            "custom_tool_call_output" => Deserialize<CustomToolCallOutputResponseItem>(doc, options),
            "web_search_call" => Deserialize<WebSearchCallResponseItem>(doc, options),
            "ghost_snapshot" => Deserialize<GhostSnapshotResponseItem>(doc, options),
            "compaction" => Deserialize<CompactionResponseItem>(doc, options),
            "compaction_summary" => Deserialize<CompactionResponseItem>(doc, options),
            _ => new OtherResponseItem(doc.RootElement.Clone())
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ResponseItem value,
        JsonSerializerOptions options)
    {
        if (value is OtherResponseItem other)
        {
            other.Raw.WriteTo(writer);
            return;
        }

        var typeName = GetTypeName(value);
        var element = JsonSerializer.SerializeToElement(value, value.GetType(), options);
        writer.WriteStartObject();
        writer.WriteString("type", typeName);
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("type"))
            {
                continue;
            }
            property.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static string GetTypeName(ResponseItem value) => value switch
    {
        MessageResponseItem => "message",
        ReasoningResponseItem => "reasoning",
        LocalShellCallResponseItem => "local_shell_call",
        FunctionCallResponseItem => "function_call",
        FunctionCallOutputResponseItem => "function_call_output",
        CustomToolCallResponseItem => "custom_tool_call",
        CustomToolCallOutputResponseItem => "custom_tool_call_output",
        WebSearchCallResponseItem => "web_search_call",
        GhostSnapshotResponseItem => "ghost_snapshot",
        CompactionResponseItem => "compaction",
        _ => "other"
    };

    private static T Deserialize<T>(JsonDocument doc, JsonSerializerOptions options)
        where T : ResponseItem
    {
        return JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), options)
               ?? throw new JsonException($"Unable to deserialize {typeof(T).Name}.");
    }
}

internal sealed class LocalShellStatusConverter : JsonConverter<LocalShellStatus>
{
    public override LocalShellStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "completed" => LocalShellStatus.Completed,
            "in_progress" => LocalShellStatus.InProgress,
            "incomplete" => LocalShellStatus.Incomplete,
            _ => throw new JsonException($"Unknown LocalShellStatus '{value}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        LocalShellStatus value,
        JsonSerializerOptions options)
    {
        var text = value switch
        {
            LocalShellStatus.Completed => "completed",
            LocalShellStatus.InProgress => "in_progress",
            LocalShellStatus.Incomplete => "incomplete",
            _ => "incomplete"
        };
        writer.WriteStringValue(text);
    }
}
