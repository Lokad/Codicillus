using Lokad.Codicillus.Core;
using Lokad.Codicillus.Protocol;
using Xunit;

namespace Lokad.Codicillus.Tests;

public sealed class PromptBuilderTests
{
    [Fact]
    public void BuildInitialContext_OrdersMessages()
    {
        var context = new TurnContext
        {
            WorkingDirectory = "C:\\repo",
            ApprovalPolicy = AskForApproval.OnRequest,
            SandboxPolicy = new DangerFullAccessPolicy(),
            Shell = new ShellInfo(ShellType.PowerShell),
            DeveloperInstructions = "dev",
            UserInstructions = "user"
        };

        var items = PromptBuilder.BuildInitialContext(context);

        var developer = Assert.IsType<MessageResponseItem>(items[0]);
        Assert.Equal("developer", developer.Role);
        Assert.Equal("dev", ((InputTextContent)developer.Content[0]).Text);

        var userInstructions = Assert.IsType<MessageResponseItem>(items[1]);
        Assert.Equal("user", userInstructions.Role);
        Assert.StartsWith("# AGENTS.md instructions for C:\\repo", ((InputTextContent)userInstructions.Content[0]).Text);

        var env = Assert.IsType<MessageResponseItem>(items[2]);
        Assert.Equal("user", env.Role);
        Assert.Contains("<environment_context>", ((InputTextContent)env.Content[0]).Text);
    }
}
