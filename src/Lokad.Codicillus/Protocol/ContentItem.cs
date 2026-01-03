using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InputTextContent), "input_text")]
[JsonDerivedType(typeof(InputImageContent), "input_image")]
[JsonDerivedType(typeof(OutputTextContent), "output_text")]
public abstract record ContentItem;

public sealed record InputTextContent(
    [property: JsonPropertyName("text")] string Text
) : ContentItem;

public sealed record InputImageContent(
    [property: JsonPropertyName("image_url")] string ImageUrl
) : ContentItem;

public sealed record OutputTextContent(
    [property: JsonPropertyName("text")] string Text
) : ContentItem;
