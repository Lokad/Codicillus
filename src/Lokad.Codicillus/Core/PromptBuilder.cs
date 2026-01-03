using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Core;

public static class PromptBuilder
{
    private const string UserInstructionsPrefix = "# AGENTS.md instructions for ";

    public static IReadOnlyList<ResponseItem> BuildInitialContext(TurnContext context)
    {
        var items = new List<ResponseItem>();
        if (!string.IsNullOrWhiteSpace(context.DeveloperInstructions))
        {
            items.Add(new MessageResponseItem(
                "developer",
                [new InputTextContent(context.DeveloperInstructions!)],
                null));
        }

        if (!string.IsNullOrWhiteSpace(context.UserInstructions))
        {
            items.Add(BuildUserInstructionsMessage(context.WorkingDirectory, context.UserInstructions!));
        }

        var env = context.BuildEnvironmentContext();
        items.Add(new MessageResponseItem(
            "user",
            [new InputTextContent(env.SerializeToXml())],
            null));

        return items;
    }

    public static ResponseItem BuildUserInputMessage(IEnumerable<UserInput> inputs)
    {
        var content = inputs.Select(ToContentItem).ToList();
        return new MessageResponseItem("user", content, null);
    }

    private static ResponseItem BuildUserInstructionsMessage(string directory, string instructions)
    {
        var text = $"{UserInstructionsPrefix}{directory}\n\n<INSTRUCTIONS>\n{instructions}\n</INSTRUCTIONS>";
        return new MessageResponseItem("user", [new InputTextContent(text)], null);
    }

    private static ContentItem ToContentItem(UserInput input) =>
        input switch
        {
            UserInputText text => new InputTextContent(text.Text),
            UserInputImage image => new InputImageContent(image.ImageUrl),
            _ => new InputTextContent(string.Empty)
        };
}
