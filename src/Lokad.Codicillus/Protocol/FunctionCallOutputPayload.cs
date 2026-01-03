using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

[JsonConverter(typeof(FunctionCallOutputPayloadConverter))]
public sealed record FunctionCallOutputPayload
{
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<FunctionCallOutputContentItem>? ContentItems { get; init; }
    public bool? Success { get; init; }
}

internal sealed class FunctionCallOutputPayloadConverter : JsonConverter<FunctionCallOutputPayload>
{
    public override FunctionCallOutputPayload? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var items = JsonSerializer.Deserialize<List<FunctionCallOutputContentItem>>(
                ref reader,
                options);
            if (items is null)
            {
                return new FunctionCallOutputPayload();
            }

            var content = JsonSerializer.Serialize(items, options);
            return new FunctionCallOutputPayload
            {
                Content = content,
                ContentItems = items
            };
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var content = reader.GetString() ?? string.Empty;
            return new FunctionCallOutputPayload { Content = content };
        }

        var raw = JsonDocument.ParseValue(ref reader);
        return new FunctionCallOutputPayload { Content = raw.RootElement.GetRawText() };
    }

    public override void Write(
        Utf8JsonWriter writer,
        FunctionCallOutputPayload value,
        JsonSerializerOptions options)
    {
        if (value.ContentItems is not null)
        {
            JsonSerializer.Serialize(writer, value.ContentItems, options);
            return;
        }

        writer.WriteStringValue(value.Content ?? string.Empty);
    }
}
