using Lokad.Codicillus.Models;

namespace Lokad.Codicillus.Core;

public sealed record CodicillusSessionOptions
{
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();
    public AskForApproval ApprovalPolicy { get; init; } = AskForApproval.OnRequest;
    public SandboxPolicy SandboxPolicy { get; init; } = new DangerFullAccessPolicy();
    public ShellInfo Shell { get; init; } = new(ShellType.PowerShell);
    public string? DeveloperInstructions { get; init; }
    public string? UserInstructions { get; init; }
    public string Model { get; init; } = "gpt-5.2-codex";
    public ModelFamily? ModelFamilyOverride { get; init; }
    public TruncationPolicy? TruncationPolicyOverride { get; init; }
    public bool EnableShellTool { get; init; } = true;
    public bool EnableApplyPatchTool { get; init; } = true;
    public bool EnableViewImageTool { get; init; } = true;
}
