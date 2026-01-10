# Purpose: verify prompt prefix caching by forcing a large AGENTS.md and checking cached input tokens across turns.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$cliProject = Join-Path $repoRoot "src\\Lokad.Codicillus.Cli\\Lokad.Codicillus.Cli.csproj"
$tempRoot = Join-Path $PSScriptRoot ".tmp"
$tempDir = Join-Path $tempRoot "cached-prefix"

if (-not $env:OPENAI_API_KEY) {
    throw "OPENAI_API_KEY is not set."
}

if (Test-Path $tempDir) {
    Remove-Item -Recurse -Force $tempDir
}

New-Item -ItemType Directory -Path $tempDir | Out-Null

$wordCount = 1600
$words = 1..$wordCount | ForEach-Object { "token$_" }
$body = $words -join " "
$agentsContent = @"
Mock AGENTS instructions for cached prefix testing.
$body
"@

Set-Content -Path (Join-Path $tempDir "AGENTS.md") -Value $agentsContent

$prompts = @(
    "Hello",
    "Second ping",
    "Third ping"
)

$inputLines = $prompts + "exit"
$output = $inputLines | & dotnet run --no-build --project $cliProject -- --model gpt-5.1-codex-mini --cwd $tempDir --show-usage
$usageLines = $output | Where-Object { $_ -like "usage input=*" }

if ($usageLines.Count -lt $prompts.Count) {
    throw "Missing usage lines; expected $($prompts.Count), got $($usageLines.Count)."
}

$usageLines | ForEach-Object { Write-Host $_ }

$cachedCounts = $usageLines | ForEach-Object {
    $match = [regex]::Match($_, 'cached=([0-9]+)')
    if ($match.Success) { [int]$match.Groups[1].Value } else { 0 }
}

if ($cachedCounts[0] -ne 0) {
    Write-Warning "First turn reported cached=$($cachedCounts[0]); cache may already be warm."
}

$laterCached = $cachedCounts | Select-Object -Skip 1 | Where-Object { $_ -gt 0 }
if (-not $laterCached) {
    throw "Expected cached tokens on later turns; got: $($cachedCounts -join ', ')."
}

Write-Host "Cached prefix probe passed."
