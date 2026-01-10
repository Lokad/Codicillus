# Tests

## Cached Prefix Probe

This probe validates prompt prefix caching for repeated turns by forcing a large AGENTS.md payload and inspecting cached input tokens.

Prerequisites:
- `OPENAI_API_KEY` is set in the environment.
- `gpt-5.1-codex-mini` is available for your account.

Build once from the repo root:

    dotnet restore --tl:off -v minimal
    dotnet build --tl:off --nologo -v minimal

Run the probe:

    .\tests\Run-CachedPrefixProbe.ps1

Expected output includes a usage line per prompt like:

    usage input=1800 cached=0 output=40 total=1840
    usage input=1820 cached=1024 output=38 total=1858

The first turn is expected to show `cached=0`, but it may be non-zero if the cache is already warm. Later turns should report a non-zero cached count.
