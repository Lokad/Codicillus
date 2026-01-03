Lokad.Codicillus

Lokad.Codicillus is a minimal, provider-agnostic coding agent core aligned with the Codex CLI prompt and tool protocol.

Build and test
- `dotnet restore --tl:off -v minimal`
- `dotnet build --tl:off --nologo -v minimal`
- `dotnet test --tl:off --nologo -v minimal --no-build`

CLI usage
- REPL: `dotnet run --project src/Lokad.Codicillus.Cli`
- Single-shot: `dotnet run --project src/Lokad.Codicillus.Cli -- --once "list files"`
- YOLO passthrough: `dotnet run --project src/Lokad.Codicillus.Cli -- --yolo --once "Get-ChildItem -Force"`

Logging hooks
- Implement `ICodicillusLogger` to receive model and tool events.
