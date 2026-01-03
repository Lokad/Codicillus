using McMaster.Extensions.CommandLineUtils;
using Lokad.Codicillus.Abstractions;
using Lokad.Codicillus.Core;
using Lokad.Codicillus.Models;
using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;

namespace Lokad.Codicillus.Cli;

[Command(Name = "codicillus", Description = "Minimal Codicillus CLI")]
public sealed class RootCommand
{
    [Option("--model <MODEL>", Description = "Model slug to use.")]
    public string Model { get; } = "gpt-5.1-codex-max";

    [Option("--cwd <PATH>", Description = "Working directory for the session.")]
    public string? WorkingDirectory { get; }

    [Option("--once <TEXT>", Description = "Run a single prompt and exit.")]
    public string? Once { get; }

    public async Task<int> OnExecuteAsync(CancellationToken cancellationToken)
    {
        IModelAdapter adapter;
        try
        {
            adapter = CreateModelAdapter();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        var modelFamily = ModelCatalog.FindFamilyForModel(Model) with
        {
            ApplyPatchToolType = ApplyPatchToolType.Function
        };
        var options = new CodicillusSessionOptions
        {
            Model = Model,
            WorkingDirectory = WorkingDirectory ?? Directory.GetCurrentDirectory(),
            SandboxPolicy = new DangerFullAccessPolicy(),
            Shell = new ShellInfo(OperatingSystem.IsWindows() ? ShellType.PowerShell : ShellType.Bash),
            ModelFamilyOverride = modelFamily,
            DeveloperInstructions = "The apply_patch tool is exposed as a function that takes JSON input: {\"input\":\"...\"}."
        };
        var tools = new LocalToolExecutor(options.WorkingDirectory);
        var session = new CodicillusSession(adapter, tools, options);

        if (!string.IsNullOrWhiteSpace(Once))
        {
            if (!await TryRunPromptAsync(session, Once!, cancellationToken))
            {
                return 1;
            }
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
            if (!await TryRunPromptAsync(session, line, cancellationToken))
            {
                return 1;
            }
        }
        return 0;
    }

    private IModelAdapter CreateModelAdapter()
    {
        return new OpenAIModelAdapter(Model, LoadApiKeyFromEnv());
    }

    private static string LoadApiKeyFromEnv()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is not set.");
        }
        return apiKey;
    }

    private static async Task<bool> TryRunPromptAsync(
        CodicillusSession session,
        string input,
        CancellationToken cancellationToken)
    {
        try
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
                case ToolCallSessionEvent toolCall:
                    Console.WriteLine($"[model->tool:{toolCall.Call.CallId}] {FormatToolCall(toolCall.Call)}");
                    break;
            }
        }
        return true;
        }
        catch (System.ClientModel.ClientResultException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
    }

    private static string FormatToolCall(ToolCall call)
    {
        return call switch
        {
            FunctionToolCall functionCall => $"{functionCall.Name} {functionCall.ArgumentsJson}",
            CustomToolCall customCall => $"{customCall.Name} {customCall.Input}",
            _ => "unknown tool call"
        };
    }
}

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        return CommandLineApplication.ExecuteAsync<RootCommand>(args);
    }
}
