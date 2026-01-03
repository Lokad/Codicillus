# Lokad.Codicillus

Lokad.Codicillus is a minimal, provider-agnostic coding agent core aligned with the Codex CLI prompt and tool protocol. It provides the session orchestration and protocol surface you need to plug a model adapter and a pluggable environment into a .NET host.

## Highlights

- Codex-aligned prompt assembly and tool schemas.
- Provider-agnostic `IModelAdapter` with streaming and compaction hooks.
- Pluggable tool execution with a local pass-through implementation.
- Small, testable API surface with optional logging.

## Architecture

- `CodicillusSession` builds the prompt from system templates, environment context, and conversation history.
- The session streams model events, detects tool calls, executes tools, and appends outputs before continuing.
- Built-in tools mirror Codex CLI (`shell`, `shell_command`, `apply_patch`, `view_image`) and can be replaced.
- Prompt cache keys are used when the adapter supports them to maximize shared-prefix reuse.

## Usage

### Library (terse)

```csharp
using Lokad.Codicillus.Abstractions;
using Lokad.Codicillus.Core;
using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;

IModelAdapter adapter = /* your adapter */;
var tools = new LocalToolExecutor();
var options = new CodicillusSessionOptions
{
    Model = "gpt-5.1-codex-max",
    WorkingDirectory = Directory.GetCurrentDirectory()
};

var session = new CodicillusSession(adapter, tools, options);
var input = new UserInput[] { new UserInputText("list files") };
await foreach (var evt in session.RunTurnAsync(input, CancellationToken.None))
{
    if (evt is ModelSessionEvent { Event: ResponseOutputItemDoneEvent output } &&
        output.Item is MessageResponseItem message &&
        message.Role == "assistant")
    {
        Console.WriteLine(Compaction.ContentItemsToText(message.Content));
    }
}
```

### CLI

- REPL: `dotnet run --project src/Lokad.Codicillus.Cli`
- Single-shot: `dotnet run --project src/Lokad.Codicillus.Cli -- --once "list files"`

### CLI authentication

- The CLI reads credentials from the `OPENAI_API_KEY` environment variable.
- If it is missing or invalid, the CLI prints an error and exits.

## Logging hooks

- Implement `ICodicillusLogger` to receive model and tool events.

## Build and test

- `dotnet restore --tl:off -v minimal`
- `dotnet build --tl:off --nologo -v minimal`
- `dotnet test --tl:off --nologo -v minimal --no-build`

## Repository layout

- `src/Lokad.Codicillus`: core library implementation.
- `src/Lokad.Codicillus.Cli`: minimal CLI used for validation.
- `tests/Lokad.Codicillus.Tests`: xUnit test suite covering protocol and behavior invariants.
