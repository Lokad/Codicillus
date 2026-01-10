# Restore Cached Prefix Parity and Validate Prompt Cache Reuse

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan is governed by `code:\Codicillus\.agent\PLANS.md`, and must remain consistent with its requirements.

## Purpose / Big Picture

Codicillus should reuse cached prompt prefixes the same way the original Codex CLI does, because that is a major driver of speed and cost efficiency for long, repeated context (for example large AGENTS.md instructions). After this change, running a short sequence of prompts against a workspace that contains a very large AGENTS.md file should report substantial cached input tokens on the second and later turns, matching the Codex behavior. The user-visible proof is the cached token counters printed by a manual probe script.

## Progress

- [x] (2026-01-10 10:08Z) Created initial ExecPlan for cached prefix parity and validation.
- [ ] Compare Codicillus and openai-codex prompt caching implementations and record any divergences.
- [ ] Add prompt cache key and cached token usage mapping in the OpenAI adapter, then verify usage reporting.
- [ ] Add a manual cached-prefix probe (script + usage logging) and document how to run it.
- [ ] Validate the probe against gpt-5.1-codex-mini and record observations.

## Surprises & Discoveries

None yet.

## Decision Log

- Decision: Keep the prompt cache key stable by reusing the session conversation id, matching openai-codex behavior.
  Rationale: Codex uses the conversation id as prompt_cache_key for Responses API, which should maximize reusable prefix caching across turns.
  Date/Author: 2026-01-10 (Codex).

- Decision: Add a manual probe script and optional CLI usage output instead of a unit test.
  Rationale: Cached input tokens require live API behavior and cannot be validated reliably with hermetic unit tests.
  Date/Author: 2026-01-10 (Codex).

## Outcomes & Retrospective

Not started yet.

## Context and Orientation

Codicillus is a .NET solution with core prompt assembly in `src/Lokad.Codicillus/Core` and the OpenAI Responses API adapter in `src/Lokad.Codicillus.Cli/OpenAIModelAdapter.cs`. The session constructs a `ModelPrompt` with a `PromptCacheKey` set to the session `ConversationId`, but the adapter currently ignores that field and only sends instructions, input, and tools to the OpenAI client. The protocol layer defines `TokenUsage` with a `CachedInputTokens` field, yet the adapter only maps basic input/output/total tokens from the OpenAI SDK. The original Codex CLI implementation sets `prompt_cache_key` on Responses API requests and records cached input tokens from the response usage. The manual test harness must exercise a large AGENTS.md payload and verify that cached input tokens are reported by the OpenAI API and surfaced by Codicillus.

Key files to inspect and modify are `src/Lokad.Codicillus/Core/CodicillusSession.cs`, `src/Lokad.Codicillus/Core/ModelPrompt.cs`, `src/Lokad.Codicillus.Cli/OpenAIModelAdapter.cs`, and `tests/` (new probe script and documentation). Reference behavior lives in `code:\openai-codex\codex-rs\core\src\client.rs` and `code:\openai-codex\codex-rs\codex-api\src\requests\responses.rs`, where `prompt_cache_key` is set on Responses API calls.

## Plan of Work

First, compare Codicillus and openai-codex implementations around prompt caching. Confirm how Codex sets `prompt_cache_key` (in Responses requests) and how it reads cached input tokens, then identify the divergence in Codicillus (missing prompt cache key on the OpenAI client and missing cached token mapping). Record the exact fields or properties needed in the OpenAI .NET SDK for setting the prompt cache key and reading cached token details.

Next, update `OpenAIModelAdapter` to pass `prompt.PromptCacheKey` into the Responses request and to map cached input tokens (and, if available, reasoning output tokens) into `TokenUsage`. If the OpenAI SDK uses a nested structure for input token details, map `CachedTokenCount` (or equivalent) into `TokenUsage.CachedInputTokens`. If the SDK uses a different name, document and use the exact property. Ensure this mapping is null-safe so responses without usage details still succeed.

Then, add an integration-style probe that is explicitly not a unit test. The simplest approach is to add a CLI flag (for example `--show-usage`) that prints token usage on `ResponseCompletedEvent` and a PowerShell script under `tests/` that creates a temporary workspace containing a large mock `AGENTS.md` (well over 1,024 tokens), runs a short series of prompts with `--once` using `gpt-5.1-codex-mini`, and captures the printed usage lines. The script should assert or visibly show that cached input tokens are zero on the first turn and non-zero (ideally at least 1,024) on subsequent turns.

Finally, update or create a `tests/README.md` describing the cached prefix probe, the expected outputs, and how to run it. Include the model requirement and the dependency on `OPENAI_API_KEY`.

## Concrete Steps

Work from `code:\Codicillus` unless noted otherwise.

Inspect Codex prompt cache behavior and map it to Codicillus:

    rg -n "prompt_cache_key" -S code:\openai-codex\codex-rs
    Get-Content -Path code:\openai-codex\codex-rs\core\src\client.rs
    Get-Content -Path code:\openai-codex\codex-rs\codex-api\src\requests\responses.rs
    Get-Content -Path code:\Codicillus\src\Lokad.Codicillus.Cli\OpenAIModelAdapter.cs

Update the OpenAI adapter to pass the prompt cache key and map cached token usage. Use the OpenAI SDK types available in the solution to find the exact property name for the prompt cache key and cached token details, and then implement it in `OpenAIModelAdapter`.

Add a CLI flag (for example `--show-usage`) and print usage on each `ResponseCompletedEvent` so the probe can observe cached input tokens. Keep the output stable and single-line, such as: `usage input=1234 cached=1024 output=56 total=1290`.

Create a manual probe script under `tests/` (for example `tests/Run-CachedPrefixProbe.ps1`) that:

    - Creates a temporary workspace directory (for example `tests/.tmp/cached-prefix`).
    - Writes a mock `AGENTS.md` with at least 1,500 simple English words so the prompt prefix exceeds 1,024 tokens.
    - Runs Codicillus twice or three times with `--once`, `--cwd` set to the temp workspace, `--model gpt-5.1-codex-mini`, and the new `--show-usage` flag.
    - Prints the usage lines and a short pass/fail summary based on cached input tokens on turn 2 and later.

Update `tests/README.md` with the new probe instructions and expected results.

Build and run the probe using the standard .NET commands with `--tl:off`:

    dotnet restore --tl:off -v minimal
    dotnet build --tl:off --nologo -v minimal
    ./tests/Run-CachedPrefixProbe.ps1

## Validation and Acceptance

Acceptance is met when the cached-prefix probe shows the following behavior against `gpt-5.1-codex-mini` with a large mock AGENTS.md file:

- The first turn reports `cached=0` (or no cached tokens), because there is no cache yet.
- The second and later turns report `cached` greater than zero, with a target of at least 1,024 cached tokens given the prompt prefix size.
- The output includes the `prompt_cache_key` effect implicitly through cached token reuse; no changes should be required to the prompt content between turns beyond the user input line.

If cached tokens remain at zero on later turns, the adapter or request is still missing the prompt cache key or token usage mapping and must be fixed before acceptance.

## Idempotence and Recovery

The probe script should be safe to re-run. It must recreate or clean its temporary workspace before writing the mock AGENTS.md. If the API call fails, the script should exit with a non-zero code and leave the temp workspace intact for inspection. No changes to production data are required.

## Artifacts and Notes

After a successful run, capture a short excerpt like the following in the README or plan updates:

    usage input=1800 cached=0 output=40 total=1840
    usage input=1820 cached=1024 output=38 total=1858

## Interfaces and Dependencies

`OpenAIModelAdapter.StreamAsync` in `src/Lokad.Codicillus.Cli/OpenAIModelAdapter.cs` must set the prompt cache key on `CreateResponseOptions` (or the equivalent OpenAI SDK property) using `ModelPrompt.PromptCacheKey`. It must also map cached input tokens from the OpenAI SDK usage object into `Lokad.Codicillus.Protocol.TokenUsage.CachedInputTokens`. If the SDK exposes reasoning or cached token details under a nested type (for example `InputTokenDetails.CachedTokenCount`), that type must be referenced explicitly and kept null-safe. The new CLI flag should be added in `src/Lokad.Codicillus.Cli/Program.cs` with minimal additional parsing.

Plan change notes: 2026-01-10 Initial creation of the ExecPlan for cached prefix parity and validation.
