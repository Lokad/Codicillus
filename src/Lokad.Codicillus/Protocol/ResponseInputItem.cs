using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ResponseInputMessageItem), "message")]
[JsonDerivedType(typeof(ResponseInputFunctionCallOutputItem), "function_call_output")]
[JsonDerivedType(typeof(ResponseInputCustomToolCallOutputItem), "custom_tool_call_output")]
public abstract record ResponseInputItem;

public sealed record ResponseInputMessageItem(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] IReadOnlyList<ContentItem> Content
) : ResponseInputItem;

public sealed record ResponseInputFunctionCallOutputItem(
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("output")] FunctionCallOutputPayload Output
) : ResponseInputItem;

public sealed record ResponseInputCustomToolCallOutputItem(
    [property: JsonPropertyName("call_id")] string CallId,
    [property: JsonPropertyName("output")] string Output
) : ResponseInputItem;
