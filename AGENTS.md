## Turn off the .NET “terminal logger”

Use `--tl:off` avoids dynamic output and progress rendering.

```powershell
dotnet restore --tl:off -v minimal
dotnet build   --tl:off --nologo -v minimal
dotnet test    --tl:off --nologo -v minimal --no-build
```

## Commit hygiene

- Before every commit, review whether `AGENTS.md` needs updates for new workflows or constraints.
- After every complex commit, review the full diff and the commit content to confirm there are no accidental or irrelevant changes.
- Commit messages should avoid personal or machine-specific references. Use a single-line message for small changes and a concise multi-line message for complex changes.

## ExecPlans

When writing complex features or significant refactors, use an ExecPlan (as described in .agent/PLANS.md) from design to implementation.
