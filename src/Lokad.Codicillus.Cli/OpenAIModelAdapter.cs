using System.Runtime.CompilerServices;
using System.Text.Json;
using Lokad.Codicillus.Abstractions;
using Lokad.Codicillus.Core;
using Lokad.Codicillus.Protocol;
using OpenAI;
using OpenAI.Responses;
using CodicillusCustomToolCallOutputResponseItem = Lokad.Codicillus.Protocol.CustomToolCallOutputResponseItem;
using CodicillusCustomToolCallResponseItem = Lokad.Codicillus.Protocol.CustomToolCallResponseItem;
using CodicillusFunctionCallOutputResponseItem = Lokad.Codicillus.Protocol.FunctionCallOutputResponseItem;
using CodicillusFunctionCallResponseItem = Lokad.Codicillus.Protocol.FunctionCallResponseItem;
using CodicillusMessageResponseItem = Lokad.Codicillus.Protocol.MessageResponseItem;
using CodicillusReasoningResponseItem = Lokad.Codicillus.Protocol.ReasoningResponseItem;
using CodicillusResponseItem = Lokad.Codicillus.Protocol.ResponseItem;
using OpenAIResponseItem = OpenAI.Responses.ResponseItem;

namespace Lokad.Codicillus.Cli;

internal sealed class OpenAIModelAdapter : IModelAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private readonly ResponsesClient _client;
    private readonly string _model;

    public OpenAIModelAdapter(string model, string apiKey)
    {
        _model = model;
        _client = new ResponsesClient(model, apiKey);

        Capabilities = new ModelCapabilities
        {
            SupportsPromptCacheKey = true,
            SupportsParallelToolCalls = true
        };
    }

    public ModelCapabilities Capabilities { get; }

    public async IAsyncEnumerable<ResponseEvent> StreamAsync(
        ModelPrompt prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var options = new CreateResponseOptions
        {
            Instructions = prompt.Instructions,
            StreamingEnabled = true,
            ParallelToolCallsEnabled = prompt.ParallelToolCalls
        };
        // The Responses API conversation id must come from a prior response;
        // do not map the prompt cache key directly.

        foreach (var item in prompt.Input)
        {
            options.InputItems.Add(ToOpenAIResponseItem(item));
        }

        foreach (var tool in prompt.Tools)
        {
            options.Tools.Add(ToOpenAIResponseTool(tool));
        }

        await foreach (var update in _client.CreateResponseStreamingAsync(options, cancellationToken))
        {
            switch (update)
            {
                case StreamingResponseCreatedUpdate:
                    yield return new ResponseCreatedEvent();
                    break;
                case StreamingResponseOutputTextDeltaUpdate delta:
                    if (!string.IsNullOrEmpty(delta.Delta))
                    {
                        yield return new ResponseOutputTextDeltaEvent(delta.Delta);
                    }
                    break;
                case StreamingResponseOutputItemAddedUpdate added:
                    {
                        var item = FromOpenAIResponseItem(added.Item);
                        if (item is not null)
                        {
                            yield return new ResponseOutputItemAddedEvent(item);
                        }
                        break;
                    }
                case StreamingResponseOutputItemDoneUpdate done:
                    {
                        var item = FromOpenAIResponseItem(done.Item);
                        if (item is not null)
                        {
                            yield return new ResponseOutputItemDoneEvent(item);
                        }
                        break;
                    }
                case StreamingResponseCompletedUpdate completed:
                    yield return new ResponseCompletedEvent(
                        completed.Response?.Id ?? string.Empty,
                        ToTokenUsage(completed.Response?.Usage));
                    break;
                case StreamingResponseFailedUpdate failed:
                    throw new InvalidOperationException(
                        $"OpenAI response failed: {failed.Response?.Error?.Message ?? "unknown error"}");
                case StreamingResponseIncompleteUpdate incomplete:
                    yield return new ResponseCompletedEvent(
                        incomplete.Response?.Id ?? string.Empty,
                        ToTokenUsage(incomplete.Response?.Usage));
                    break;
                case StreamingResponseErrorUpdate error:
                    var rawCode = error.Code;
                    var rawMessage = error.Message;
                    var code = string.IsNullOrWhiteSpace(rawCode) ? "unknown" : rawCode;
                    var message = string.IsNullOrWhiteSpace(rawMessage) ? "unknown error" : rawMessage;
                    if (string.IsNullOrWhiteSpace(rawCode) && string.IsNullOrWhiteSpace(rawMessage))
                    {
                        throw new InvalidOperationException(
                            $"OpenAI response error: model '{_model}' is not available or not permitted.");
                    }
                    throw new InvalidOperationException($"OpenAI response error: {code} {message}");
            }
        }
    }

    public Task<IReadOnlyList<CodicillusResponseItem>> CompactAsync(
        ModelPrompt prompt,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<CodicillusResponseItem>>(Array.Empty<CodicillusResponseItem>());
    }

    private static ResponseTool ToOpenAIResponseTool(ToolSpec tool) => tool switch
    {
        FunctionToolSpec function => ResponseTool.CreateFunctionTool(
            function.Name,
            BinaryData.FromString(JsonSerializer.Serialize(BuildJsonSchema(function.Parameters), JsonOptions)),
            function.Strict,
            function.Description),
        CustomToolSpec custom => ResponseTool.CreateFunctionTool(
            custom.Name,
            BinaryData.FromString(JsonSerializer.Serialize(BuildJsonSchema(BuildCustomToolSchema()), JsonOptions)),
            false,
            custom.Description),
        _ => throw new NotSupportedException($"Unsupported tool spec: {tool.GetType().Name}")
    };

    private static JsonSchema BuildCustomToolSchema()
    {
        return new JsonSchemaObject(
            new Dictionary<string, JsonSchema>
            {
                ["input"] = new JsonSchemaString("Tool input payload")
            },
            new[] { "input" },
            new AdditionalPropertiesBoolean(false));
    }

    private static object BuildJsonSchema(JsonSchema schema)
    {
        return schema switch
        {
            JsonSchemaBoolean boolean => BuildSimpleSchema("boolean", boolean.Description),
            JsonSchemaString str => BuildSimpleSchema("string", str.Description),
            JsonSchemaNumber number => BuildSimpleSchema("number", number.Description),
            JsonSchemaArray array => BuildArraySchema(array),
            JsonSchemaObject obj => BuildObjectSchema(obj),
            _ => new Dictionary<string, object> { ["type"] = "object" }
        };
    }

    private static Dictionary<string, object> BuildSimpleSchema(string type, string? description)
    {
        var schema = new Dictionary<string, object> { ["type"] = type };
        if (!string.IsNullOrWhiteSpace(description))
        {
            schema["description"] = description!;
        }
        return schema;
    }

    private static Dictionary<string, object> BuildArraySchema(JsonSchemaArray array)
    {
        var schema = BuildSimpleSchema("array", array.Description);
        schema["items"] = BuildJsonSchema(array.Items);
        return schema;
    }

    private static Dictionary<string, object> BuildObjectSchema(JsonSchemaObject obj)
    {
        var schema = new Dictionary<string, object> { ["type"] = "object" };
        var properties = new Dictionary<string, object>();
        foreach (var kvp in obj.Properties)
        {
            properties[kvp.Key] = BuildJsonSchema(kvp.Value);
        }
        schema["properties"] = properties;
        if (obj.Required is { Count: > 0 })
        {
            schema["required"] = obj.Required;
        }
        if (obj.AdditionalProperties is not null)
        {
            schema["additionalProperties"] = obj.AdditionalProperties switch
            {
                AdditionalPropertiesBoolean boolean => boolean.Value,
                AdditionalPropertiesSchema schemaValue => BuildJsonSchema(schemaValue.Schema),
                _ => false
            };
        }
        return schema;
    }

    private static OpenAIResponseItem ToOpenAIResponseItem(CodicillusResponseItem item) => item switch
    {
        CodicillusMessageResponseItem message => ToOpenAIMessageItem(message),
        CodicillusFunctionCallResponseItem functionCall => OpenAIResponseItem.CreateFunctionCallItem(
            functionCall.CallId,
            functionCall.Name,
            BinaryData.FromString(functionCall.Arguments)),
        CodicillusFunctionCallOutputResponseItem functionOutput => OpenAIResponseItem.CreateFunctionCallOutputItem(
            functionOutput.CallId,
            functionOutput.Output.Content),
        CodicillusCustomToolCallResponseItem customCall => OpenAIResponseItem.CreateFunctionCallItem(
            customCall.CallId,
            customCall.Name,
            BinaryData.FromString(JsonSerializer.Serialize(new { input = customCall.Input }, JsonOptions))),
        CodicillusCustomToolCallOutputResponseItem customOutput => OpenAIResponseItem.CreateFunctionCallOutputItem(
            customOutput.CallId,
            customOutput.Output),
        CodicillusReasoningResponseItem reasoning => OpenAIResponseItem.CreateReasoningItem(
            reasoning.Summary.Select(summary => ReasoningSummaryPart.CreateTextPart(summary.Text)).ToList()),
        _ => throw new NotSupportedException($"Unsupported response item: {item.GetType().Name}")
    };

    private static OpenAI.Responses.MessageResponseItem ToOpenAIMessageItem(
        CodicillusMessageResponseItem message)
    {
        var parts = message.Content.Select(ToOpenAIContentPart).ToList();
        return message.Role switch
        {
            "developer" => OpenAIResponseItem.CreateDeveloperMessageItem(parts),
            "system" => OpenAIResponseItem.CreateSystemMessageItem(parts),
            "assistant" => OpenAIResponseItem.CreateAssistantMessageItem(parts),
            _ => OpenAIResponseItem.CreateUserMessageItem(parts)
        };
    }

    private static ResponseContentPart ToOpenAIContentPart(ContentItem content) => content switch
    {
        InputTextContent input => ResponseContentPart.CreateInputTextPart(input.Text),
        InputImageContent image => CreateInputImagePart(image.ImageUrl),
        OutputTextContent output => ResponseContentPart.CreateOutputTextPart(
            output.Text,
            Array.Empty<ResponseMessageAnnotation>()),
        _ => ResponseContentPart.CreateInputTextPart(string.Empty)
    };

    private static ResponseContentPart CreateInputImagePart(string imageUrl)
    {
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return ResponseContentPart.CreateInputImagePart(uri, null);
        }

        return ResponseContentPart.CreateInputImagePart(imageUrl, null);
    }

    private static CodicillusResponseItem? FromOpenAIResponseItem(OpenAIResponseItem item) => item switch
    {
        OpenAI.Responses.MessageResponseItem message => new CodicillusMessageResponseItem(
            MapRole(message.Role),
            message.Content.Select(FromOpenAIContentPart).ToList(),
            message.Id),
        OpenAI.Responses.FunctionCallResponseItem call => new CodicillusFunctionCallResponseItem(
            call.FunctionName,
            call.FunctionArguments.ToString(),
            call.CallId,
            call.Id),
        OpenAI.Responses.FunctionCallOutputResponseItem output => new CodicillusFunctionCallOutputResponseItem(
            output.CallId,
            new FunctionCallOutputPayload
            {
                Content = output.FunctionOutput,
                Success = true
            }),
        OpenAI.Responses.ReasoningResponseItem reasoning => new CodicillusReasoningResponseItem(
            reasoning.Id ?? string.Empty,
            reasoning.SummaryParts
                .OfType<ReasoningSummaryTextPart>()
                .Select(part => new ReasoningSummaryItem("summary_text", part.Text))
                .ToList(),
            null,
            reasoning.EncryptedContent),
        _ => null
    };

    private static ContentItem FromOpenAIContentPart(ResponseContentPart part)
    {
        return part.Kind switch
        {
            ResponseContentPartKind.InputText => new InputTextContent(part.Text ?? string.Empty),
            ResponseContentPartKind.OutputText => new OutputTextContent(part.Text ?? string.Empty),
            ResponseContentPartKind.InputImage => new InputImageContent(ResolveImageUrl(part)),
            ResponseContentPartKind.Refusal => new OutputTextContent(part.Refusal ?? string.Empty),
            _ => new OutputTextContent(string.Empty)
        };
    }

    private static string ResolveImageUrl(ResponseContentPart part)
    {
        var imageUrlProp = part.GetType().GetProperty("ImageUrl");
        if (imageUrlProp?.GetValue(part) is string imageUrl && !string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }
        if (!string.IsNullOrWhiteSpace(part.InputImageFileId))
        {
            return part.InputImageFileId;
        }
        return string.Empty;
    }

    private static string MapRole(MessageRole role) => role switch
    {
        MessageRole.Developer => "developer",
        MessageRole.System => "system",
        MessageRole.Assistant => "assistant",
        _ => "user"
    };

    private static TokenUsage? ToTokenUsage(ResponseTokenUsage? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return new TokenUsage
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount,
            TotalTokens = usage.TotalTokenCount
        };
    }

}
