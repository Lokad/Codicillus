# Design Lokad.Codicillus core aligned with Codex

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan must be maintained in accordance with `code:\Codicillus\.agent\PLANS.md`.
After each commit, proceed automatically to the next step without waiting for manual confirmation.

## Purpose / Big Picture

Implement the minimal, provider-agnostic core of Codex in a new .NET 10 library so a .NET host can plug in a coding agent with the same prompt structure, tool protocol, streaming events, and prompt-cache (prefix reuse) behavior used by the Codex CLI. After this change, a host can create a Codicillus session, send user turns, stream assistant output/tool calls, and execute tools through a pluggable environment while preserving Codex’s shared-prefix caching and context compaction behavior.

## Progress

- [x] (2026-01-03 22:30Z) Drafted ExecPlan with Codex core alignment notes.
- [ ] Define public API surface for Codicillus core (session, model adapter, tools, env, logging).
- [ ] Port Codex protocol types and prompt assembly into C#, including prompt-cache hooks.
- [ ] Implement context history, truncation, and compaction (local + optional remote).
- [ ] Add xUnit suite, CLI sample, and validate build.

## Surprises & Discoveries

- Observation: Codex injects environment context as a user message containing XML, not a structured API field.
  Evidence: `code:\openai-codex\codex-rs\core\src\environment_context.rs`.
- Observation: Prompt prefix reuse uses the Responses API `prompt_cache_key`, set to the conversation id.
  Evidence: `code:\openai-codex\codex-rs\core\src\client.rs`.
- Observation: Initial context always prepends developer instructions, user instructions, and environment context items before any user input.
  Evidence: `code:\openai-codex\codex-rs\core\src\codex.rs`.
- Observation: Local context compaction uses a summarization prompt and then rebuilds history as initial context + selected user messages + summary.
  Evidence: `code:\openai-codex\codex-rs\core\src\compact.rs`.

## Decision Log

- Decision: Implement a single NuGet package with core types plus a default local environment adapter for pass-through shell execution.
  Rationale: The user wants one package; a minimal built-in environment reduces friction while still allowing custom pluggable hosts.
  Date/Author: 2026-01-03 / Codex
- Decision: Mirror Codex’s Responses/Chat stream event model (`ResponseEvent`) and prompt item schema (`ResponseItem`, `ContentItem`) as the canonical internal protocol.
  Rationale: This is the core alignment point for prompt reuse, tool calls, and model streaming interoperability.
  Date/Author: 2026-01-03 / Codex
- Decision: Embed Codex prompt templates and compaction prompts as resources rather than re-typing them.
  Rationale: Ensures byte-for-byte alignment with Codex prompt prefixes, improving cache hits.
  Date/Author: 2026-01-03 / Codex
- Decision: Expose context compaction as an optional capability in the model adapter, with a local fallback that uses the summarization prompt.
  Rationale: Some models support remote compaction; others do not, but Codicillus should still compact locally.
  Date/Author: 2026-01-03 / Codex
- Decision: Provide pluggable logging hooks without committing to a logging implementation.
  Rationale: Logging is not a target but must be possible; hosting apps choose their own logger.
  Date/Author: 2026-01-03 / Codex

## Outcomes & Retrospective

No implementation yet.

## Context and Orientation

The repository is empty aside from `code:\Codicillus\AGENTS.md` and `code:\Codicillus\.agent\PLANS.md`. The reference implementation lives in `code:\openai-codex`, primarily in the Rust crates `codex-rs/core`, `codex-rs/protocol`, and `codex-rs/codex-api`. The C# library will be created under `code:\Codicillus\src\Lokad.Codicillus` and target .NET 10. The core behavior to mirror is: prompt assembly (including base instructions and environment context), tool schemas, tool call parsing, streaming response events, context history maintenance, output truncation, and context compaction.

Key terms used in this plan:

“Response item” means a JSON-serializable object representing a model message or tool call, matching Codex’s `ResponseItem` variants from `code:\openai-codex\codex-rs\protocol\src\models.rs`.

“Prompt cache key” means the `prompt_cache_key` field used by the OpenAI Responses API to reuse shared prefixes, as set in `code:\openai-codex\codex-rs\core\src\client.rs`.

“Context compaction” means summarizing long conversation history and replacing prior turns with a summary, following `code:\openai-codex\codex-rs\core\src\compact.rs`.

## Plan of Work

Create a new .NET solution using the `.slnx` format and a `Lokad.Codicillus` class library that exposes a minimal but complete core agent pipeline. The library will define protocol types (`ResponseItem`, `ContentItem`, `ToolSpec`, `ResponseEvent`, etc.), prompt assembly logic, and a session engine that consumes a model adapter interface and a tool execution interface. The session will maintain conversation history using Codex’s same invariants (call/output pairing, tool output truncation) and produce follow-up tool calls when the model requests them. All commits must avoid referencing personal usernames or machine-specific details.

Port Codex prompt templates into embedded resources: `prompt.md`, `gpt_5_codex_prompt.md`, `gpt_5_1_prompt.md`, `gpt_5_2_prompt.md`, `gpt-5.1-codex-max_prompt.md`, `gpt-5.2-codex_prompt.md`, plus compaction templates from `code:\openai-codex\codex-rs\core\templates\compact\prompt.md` and `summary_prefix.md`. The C# code will load these resources for model family selection and compaction prompts. The goal is byte-for-byte equivalence to preserve shared-prefix caching.

Implement a `ModelFamily` and `ModelCatalog` in C# that replicates the prefix-based model mapping in `code:\openai-codex\codex-rs\core\src\models_manager\model_family.rs`. This includes context window defaults, reasoning summary support, parallel tool support, truncation policy defaults, and the base instructions to use for each family. Allow model adapters to override these via a `ModelMetadata` structure.

Implement `EnvironmentContext` serialization as XML and include it in the initial prompt items, exactly matching `code:\openai-codex\codex-rs\core\src\environment_context.rs`. The `CodicillusSession` should always prepend developer instructions, user instructions, and environment context before user input, matching `build_initial_context` in `code:\openai-codex\codex-rs\core\src\codex.rs`.

Define a provider-agnostic `IModelAdapter` interface that streams `ResponseEvent` values and exposes capabilities (supports reasoning summaries, output schema, prompt cache key, remote compaction). The adapter receives a `Prompt` with resolved instructions, tool specs, and a `PromptCacheKey` string. Codicillus will set that key to the conversation id when supported, mirroring Codex’s `prompt_cache_key` usage.

Define a minimal `IToolExecutor` (or `IAgentEnvironment`) that can execute shell commands and apply patches. Provide built-in tool specs for `shell`, `shell_command`, `apply_patch` (freeform and JSON), and `view_image`, matching schemas and descriptions from `code:\openai-codex\codex-rs\core\src\tools\spec.rs` and `code:\openai-codex\codex-rs\core\src\tools\handlers\apply_patch.rs`. The environment will be pluggable; include a simple local pass-through implementation using `System.Diagnostics.Process` for validation and examples.

Implement `ContextManager` (history) and `TruncationPolicy` ported from `code:\openai-codex\codex-rs\core\src\context_manager\history.rs` and `code:\openai-codex\codex-rs\core\src\truncate.rs`. Maintain the same call/output pairing rules and truncation markers. Compute approximate token counts based on 4 bytes/token to drive compaction thresholds.

Implement local context compaction following `code:\openai-codex\codex-rs\core\src\compact.rs`: collect user messages, truncate to a token budget, add summary prefix, and rebuild history as initial context + selected messages + summary. Also support remote compaction when the model adapter declares capability, mirroring `compact_remote.rs`.

Provide an xUnit test project with coverage for the core protocol: environment context XML serialization, prompt assembly order, tool schema JSON, truncation markers, and compaction history rebuild. Include a test adapter that emits deterministic `ResponseEvent` sequences so session logic can be exercised without a real model. Ensure tests also validate prompt-cache key propagation and logging hook invocation.

Add a small local CLI project that references `Lokad.Codicillus`. It must demonstrate the C# usage of the library and provide both a minimal REPL and a single-shot command mode, implemented with `McMaster.Extensions.CommandLineUtils`. Include a `--yolo` mode that passes through commands to the local environment via the built-in executor. The CLI’s role is validation and example usage, not a full TUI.

## Concrete Steps

Work from `code:\Codicillus` and create a new solution plus a class library project under `code:\Codicillus\src\Lokad.Codicillus`. Create `Lokad.Codicillus.slnx` in the repo root and add the project.

Copy prompt templates from `code:\openai-codex\codex-rs\core` into `code:\Codicillus\src\Lokad.Codicillus\Prompts\` and mark them as embedded resources in the csproj. Keep filenames identical for clarity and version tracking.

Implement protocol types under `code:\Codicillus\src\Lokad.Codicillus\Protocol\`:

    - ResponseItem, ResponseInputItem, ContentItem, FunctionCallOutputPayload (with string-or-array JSON semantics).
    - Tool schemas (JsonSchema, ToolSpec, ResponsesApiTool, FreeformTool) matching the Rust shapes.
    - ResponseEvent, TokenUsage, TokenUsageInfo, RateLimitSnapshot.

Implement core engine types under `code:\Codicillus\src\Lokad.Codicillus\Core\`:

    - CodicillusSession (queue of submissions -> event stream).
    - TurnContext (cwd, instructions, environment, tools config, truncation policy).
    - PromptBuilder (builds instructions + input items + tools list).
    - ContextManager + truncation helpers.
    - Optional logging hook interface (e.g., `ICodicillusLogger`) wired into session and tool execution.

Implement environment + tools under `code:\Codicillus\src\Lokad.Codicillus\Tools\`:

    - Built-in tool specs (shell, shell_command, apply_patch, view_image).
    - Tool router to detect tool calls from ResponseItem.
    - Tool execution interfaces and a local pass-through implementation.

Add tests under `code:\Codicillus\tests\Lokad.Codicillus.Tests` that validate:

    - Environment context XML matches expected tag order and casing.
    - Initial prompt items order: developer -> user instructions -> environment context -> user input.
    - Tool schema JSON for shell/apply_patch matches the Codex reference.
    - Truncation marker format and compaction history rebuild behavior.
    - Prompt cache key is set to conversation id when supported.
    - Logging hook receives tool and model events when configured.

Add a CLI project under `code:\Codicillus\src\Lokad.Codicillus.Cli` that:

    - Uses the Codicillus session API in a minimal REPL loop.
    - Supports a single-shot command mode for scripted usage.
    - Exposes `--yolo` to run with the local pass-through executor.
    - Supports selecting a model adapter stub for offline testing.

## Validation and Acceptance

Run `dotnet build --tl:off --nologo -v minimal` from `code:\Codicillus` and ensure the solution builds. Run `dotnet test --tl:off --nologo -v minimal --no-build` and confirm all tests pass.

Manual acceptance: using the provided test model adapter, create a session, send a user message, observe that the prompt built for the adapter includes developer/user/environment context items and that tool calls are routed through the environment. Trigger a compaction and confirm history rebuilds to initial context + selected messages + summary text.

## Idempotence and Recovery

All steps are additive and safe to rerun. If copying prompt templates is repeated, overwrite the existing files with identical content. If a test fails, update the relevant component and re-run only that test suite before re-running full tests.

## Artifacts and Notes

Expected prompt cache key behavior to preserve prefix reuse:

    - When the model adapter supports prompt caching, the prompt builder sets PromptCacheKey = ConversationId.ToString().
    - The adapter forwards PromptCacheKey to the provider (Responses API equivalent).

Expected environment context XML format (example):

    <environment_context>
      <cwd>C:\repo</cwd>
      <approval_policy>never</approval_policy>
      <sandbox_mode>danger-full-access</sandbox_mode>
      <network_access>enabled</network_access>
      <shell>powershell</shell>
    </environment_context>

## Interfaces and Dependencies

Define these public interfaces and types (names may be adjusted for .NET conventions but should be stable):

In `code:\Codicillus\src\Lokad.Codicillus\Abstractions\IModelAdapter.cs`:

    public interface IModelAdapter {
        ModelCapabilities Capabilities { get; }
        IAsyncEnumerable<ResponseEvent> StreamAsync(Prompt prompt, CancellationToken ct);
        Task<IReadOnlyList<ResponseItem>> CompactAsync(Prompt prompt, CancellationToken ct);
    }

In `code:\Codicillus\src\Lokad.Codicillus\Abstractions\ICodicillusLogger.cs`:

    public interface ICodicillusLogger {
        void OnModelEvent(ResponseEvent evt);
        void OnToolCall(ToolCall call);
        void OnToolResult(ToolResult result);
    }

Document this single logging interface in the public API docs and in the CLI usage notes.

In `code:\Codicillus\src\Lokad.Codicillus\Abstractions\IToolExecutor.cs`:

    public interface IToolExecutor {
        Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct);
    }

In `code:\Codicillus\src\Lokad.Codicillus\Core\CodicillusSession.cs`:

    public sealed class CodicillusSession {
        public Guid ConversationId { get; }
        public IAsyncEnumerable<SessionEvent> RunTurnAsync(UserInput[] input, CancellationToken ct);
        public Task CompactAsync(CancellationToken ct);
    }

Model prompt resources are embedded from:

    - `code:\openai-codex\codex-rs\core\prompt.md`
    - `code:\openai-codex\codex-rs\core\gpt_5_codex_prompt.md`
    - `code:\openai-codex\codex-rs\core\gpt_5_1_prompt.md`
    - `code:\openai-codex\codex-rs\core\gpt_5_2_prompt.md`
    - `code:\openai-codex\codex-rs\core\gpt-5.1-codex-max_prompt.md`
    - `code:\openai-codex\codex-rs\core\gpt-5.2-codex_prompt.md`
    - `code:\openai-codex\codex-rs\core\templates\compact\prompt.md`
    - `code:\openai-codex\codex-rs\core\templates\compact\summary_prefix.md`

When revising this plan, append a brief note here describing what changed and why.

Plan updated to add .slnx, xUnit, CLI, and logging hooks, per user request.
Plan updated to specify McMaster-based REPL plus single-shot CLI mode and documented logging interface.
