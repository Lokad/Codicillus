using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JsonSchemaBoolean), "boolean")]
[JsonDerivedType(typeof(JsonSchemaString), "string")]
[JsonDerivedType(typeof(JsonSchemaNumber), "number")]
[JsonDerivedType(typeof(JsonSchemaArray), "array")]
[JsonDerivedType(typeof(JsonSchemaObject), "object")]
public abstract record JsonSchema;

public sealed record JsonSchemaBoolean(
    [property: JsonPropertyName("description")] string? Description = null
) : JsonSchema;

public sealed record JsonSchemaString(
    [property: JsonPropertyName("description")] string? Description = null
) : JsonSchema;

public sealed record JsonSchemaNumber(
    [property: JsonPropertyName("description")] string? Description = null
) : JsonSchema;

public sealed record JsonSchemaArray(
    [property: JsonPropertyName("items")] JsonSchema Items,
    [property: JsonPropertyName("description")] string? Description = null
) : JsonSchema;

public sealed record JsonSchemaObject(
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, JsonSchema> Properties,
    [property: JsonPropertyName("required")] IReadOnlyList<string>? Required,
    [property: JsonPropertyName("additionalProperties")] AdditionalProperties? AdditionalProperties
) : JsonSchema;

[JsonConverter(typeof(AdditionalPropertiesConverter))]
public abstract record AdditionalProperties;

public sealed record AdditionalPropertiesBoolean(bool Value) : AdditionalProperties;

public sealed record AdditionalPropertiesSchema(JsonSchema Schema) : AdditionalProperties;

internal sealed class AdditionalPropertiesConverter : JsonConverter<AdditionalProperties>
{
    public override AdditionalProperties? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => new AdditionalPropertiesBoolean(true),
            JsonTokenType.False => new AdditionalPropertiesBoolean(false),
            JsonTokenType.StartObject => new AdditionalPropertiesSchema(
                JsonSerializer.Deserialize<JsonSchema>(ref reader, options)
                ?? throw new JsonException("Invalid JSON schema.")),
            _ => throw new JsonException("Invalid additionalProperties value.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        AdditionalProperties value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case AdditionalPropertiesBoolean booleanValue:
                writer.WriteBooleanValue(booleanValue.Value);
                break;
            case AdditionalPropertiesSchema schemaValue:
                JsonSerializer.Serialize(writer, schemaValue.Schema, options);
                break;
            default:
                writer.WriteBooleanValue(false);
                break;
        }
    }
}
