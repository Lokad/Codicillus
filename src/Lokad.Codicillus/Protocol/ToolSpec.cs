using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FunctionToolSpec), "function")]
[JsonDerivedType(typeof(LocalShellToolSpec), "local_shell")]
[JsonDerivedType(typeof(WebSearchToolSpec), "web_search")]
[JsonDerivedType(typeof(CustomToolSpec), "custom")]
public abstract record ToolSpec;

public sealed record FunctionToolSpec(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("strict")] bool Strict,
    [property: JsonPropertyName("parameters")] JsonSchema Parameters
) : ToolSpec;

public sealed record LocalShellToolSpec : ToolSpec;

public sealed record WebSearchToolSpec : ToolSpec;

public sealed record CustomToolSpec(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("format")] FreeformToolFormat Format
) : ToolSpec;

public sealed record FreeformToolFormat(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("syntax")] string Syntax,
    [property: JsonPropertyName("definition")] string Definition
);
