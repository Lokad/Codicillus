using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using Lokad.Codicillus.Abstractions;
using Lokad.Codicillus.Core;
using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;

namespace Lokad.Codicillus.Cli;

[Command(Name = "codicillus", Description = "Minimal Codicillus CLI")]
public sealed class RootCommand
{
    [Option("--model <MODEL>", Description = "Model slug to use.")]
    public string Model { get; } = "gpt-5.2-codex";

    [Option("--cwd <PATH>", Description = "Working directory for the session.")]
    public string? WorkingDirectory { get; }

    [Option("--once <TEXT>", Description = "Run a single prompt and exit.")]
    public string? Once { get; }

    [Option("--yolo", Description = "Generate tool calls directly from input.")]
    public bool Yolo { get; }

    [Option("--stub", Description = "Use a stub model adapter that echoes input.")]
    public bool Stub { get; }

    public async Task<int> OnExecuteAsync(CancellationToken cancellationToken)
    {
        var adapter = CreateModelAdapter();
        var tools = new LocalToolExecutor();
        var options = new CodicillusSessionOptions
        {
            Model = Model,
            WorkingDirectory = WorkingDirectory ?? Directory.GetCurrentDirectory(),
            SandboxPolicy = new DangerFullAccessPolicy(),
            Shell = new ShellInfo(OperatingSystem.IsWindows() ? ShellType.PowerShell : ShellType.Bash)
        };
        var session = new CodicillusSession(adapter, tools, options);

        if (!string.IsNullOrWhiteSpace(Once))
        {
            await RunPromptAsync(session, Once!, cancellationToken);
            return 0;
        }

        Console.WriteLine("Codicillus REPL. Type 'exit' to quit.");
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line is null || line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            await RunPromptAsync(session, line, cancellationToken);
        }
        return 0;
    }

    private IModelAdapter CreateModelAdapter()
    {
        if (Yolo)
        {
            return new YoloModelAdapter();
        }
        if (Stub)
        {
            return new EchoModelAdapter();
        }
        return new EchoModelAdapter();
    }

    private static async Task RunPromptAsync(
        CodicillusSession session,
        string input,
        CancellationToken cancellationToken)
    {
        var userInputs = new[] { new UserInputText(input) };
        await foreach (var evt in session.RunTurnAsync(userInputs, cancellationToken))
        {
            switch (evt)
            {
                case ModelSessionEvent { Event: ResponseOutputItemDoneEvent outputEvent }:
                    if (outputEvent.Item is MessageResponseItem message && message.Role == "assistant")
                    {
                        var text = Compaction.ContentItemsToText(message.Content);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            Console.WriteLine(text);
                        }
                    }
                    break;
                case ToolResultSessionEvent toolResult:
                    Console.WriteLine($"[tool:{toolResult.Result.CallId}] {FormatToolResult(toolResult.Result)}");
                    break;
            }
        }
    }

    private static string FormatToolResult(ToolResult result)
    {
        return result switch
        {
            FunctionToolResult functionResult => functionResult.Output.Content,
            CustomToolResult customResult => customResult.Output,
            _ => "unknown tool result"
        };
    }

    private sealed class EchoModelAdapter : IModelAdapter
    {
        public ModelCapabilities Capabilities { get; } = new()
        {
            SupportsPromptCacheKey = true
        };

        public async IAsyncEnumerable<ResponseEvent> StreamAsync(
            ModelPrompt prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var message = prompt.Input
                .OfType<MessageResponseItem>()
                .LastOrDefault(item => item.Role == "user");
            var text = message is null ? string.Empty : Compaction.ContentItemsToText(message.Content);
            var output = new MessageResponseItem("assistant", [new OutputTextContent(text ?? string.Empty)], null);
            yield return new ResponseOutputItemDoneEvent(output);
            yield return new ResponseCompletedEvent(Guid.NewGuid().ToString("N"), null);
            await Task.CompletedTask;
        }

        public Task<IReadOnlyList<ResponseItem>> CompactAsync(ModelPrompt prompt, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ResponseItem>>(Array.Empty<ResponseItem>());
        }
    }

    private sealed class YoloModelAdapter : IModelAdapter
    {
        public ModelCapabilities Capabilities { get; } = new()
        {
            SupportsPromptCacheKey = true
        };

        public async IAsyncEnumerable<ResponseEvent> StreamAsync(
            ModelPrompt prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (prompt.Input.OfType<FunctionCallOutputResponseItem>().Any())
            {
                var done = new MessageResponseItem("assistant", [new OutputTextContent("Command executed.")], null);
                yield return new ResponseOutputItemDoneEvent(done);
                yield return new ResponseCompletedEvent(Guid.NewGuid().ToString("N"), null);
                await Task.CompletedTask;
                yield break;
            }

            var lastUser = prompt.Input
                .OfType<MessageResponseItem>()
                .LastOrDefault(item => item.Role == "user");
            var text = lastUser is null ? string.Empty : Compaction.ContentItemsToText(lastUser.Content);
            var args = JsonSerializer.Serialize(new ShellCommandToolCallParams { Command = text ?? string.Empty });
            var toolCall = new FunctionCallResponseItem("shell_command", args, Guid.NewGuid().ToString("N"));
            yield return new ResponseOutputItemDoneEvent(toolCall);
            yield return new ResponseCompletedEvent(Guid.NewGuid().ToString("N"), null);
            await Task.CompletedTask;
        }

        public Task<IReadOnlyList<ResponseItem>> CompactAsync(ModelPrompt prompt, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ResponseItem>>(Array.Empty<ResponseItem>());
        }
    }
}

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        return CommandLineApplication.ExecuteAsync<RootCommand>(args);
    }
}
