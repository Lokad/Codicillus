using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FunctionCallOutputText), "input_text")]
[JsonDerivedType(typeof(FunctionCallOutputImage), "input_image")]
public abstract record FunctionCallOutputContentItem;

public sealed record FunctionCallOutputText(
    [property: JsonPropertyName("text")] string Text
) : FunctionCallOutputContentItem;

public sealed record FunctionCallOutputImage(
    [property: JsonPropertyName("image_url")] string ImageUrl
) : FunctionCallOutputContentItem;
