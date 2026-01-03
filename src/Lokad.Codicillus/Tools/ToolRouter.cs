using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Tools;

public static class ToolRouter
{
    public static ToolCall? TryBuildToolCall(ResponseItem item)
    {
        return item switch
        {
            FunctionCallResponseItem functionCall => new FunctionToolCall
            {
                CallId = functionCall.CallId,
                Name = functionCall.Name,
                ArgumentsJson = functionCall.Arguments
            },
            CustomToolCallResponseItem customCall => new CustomToolCall
            {
                CallId = customCall.CallId,
                Name = customCall.Name,
                Input = customCall.Input
            },
            _ => null
        };
    }
}
