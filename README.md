# Lokad.Codicillus

Lokad.Codicillus is a minimal, provider-agnostic coding agent core aligned with the Codex CLI prompt and tool protocol.

Under the hood
- The core is model-agnostic. Implement `IModelAdapter` to stream response events and optionally support context compaction.
- `CodicillusSession` builds a prompt from system templates, the running conversation, and a serialized environment context.
- If the model supports prompt cache keys, the session supplies a stable conversation id to maximize prefix reuse.
- The session loops tool calls: model emits a tool call, the executor runs it, and the output is appended before continuing.
- Built-in tools match the Codex CLI schema (`shell`, `shell_command`, `apply_patch`, `view_image`) and can be replaced.
- Logging is opt-in via `ICodicillusLogger`, so hosts can trace traffic without a built-in logging dependency.

Build and test
- `dotnet restore --tl:off -v minimal`
- `dotnet build --tl:off --nologo -v minimal`
- `dotnet test --tl:off --nologo -v minimal --no-build`

CLI usage
- REPL: `dotnet run --project src/Lokad.Codicillus.Cli`
- Single-shot: `dotnet run --project src/Lokad.Codicillus.Cli -- --once "list files"`

CLI authentication
- The CLI reads credentials from the `OPENAI_API_KEY` environment variable.
- If it is missing or invalid, the CLI prints an error and exits.

Logging hooks
- Implement `ICodicillusLogger` to receive model and tool events.
