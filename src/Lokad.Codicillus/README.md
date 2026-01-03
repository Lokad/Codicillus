Lokad.Codicillus (library)

This folder contains the core library implementation that the CLI and tests depend on.

Folders
- `Abstractions`: Public interfaces and capability contracts (`IModelAdapter`, `IToolExecutor`, `ICodicillusLogger`, etc.).
- `Core`: Session orchestration, prompt assembly, history management, truncation, and compaction helpers.
- `Models`: Model-family catalog and default behaviors per model prefix.
- `Prompts`: Embedded prompt templates aligned with Codex CLI.
- `Protocol`: Response/event/message/tool schema types that mirror the Codex protocol.
- `Tools`: Built-in tool specs, tool routing, and local tool execution support.
