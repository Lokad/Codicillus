using Lokad.Codicillus.Models;

namespace Lokad.Codicillus.Core;

public sealed record TurnContext
{
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();
    public AskForApproval ApprovalPolicy { get; init; }
    public SandboxPolicy SandboxPolicy { get; init; } = new DangerFullAccessPolicy();
    public ShellInfo Shell { get; init; } = new(ShellType.Bash);
    public string? DeveloperInstructions { get; init; }
    public string? UserInstructions { get; init; }
    public ModelFamily ModelFamily { get; init; } = ModelCatalog.FindFamilyForModel("gpt-5.2-codex");
    public TruncationPolicy TruncationPolicy { get; init; } = TruncationPolicy.Bytes(10_000);

    public EnvironmentContext BuildEnvironmentContext()
    {
        return new EnvironmentContext
        {
            Cwd = WorkingDirectory,
            ApprovalPolicy = ApprovalPolicy,
            SandboxMode = SandboxModeFor(SandboxPolicy),
            NetworkAccess = NetworkAccessFor(SandboxPolicy),
            WritableRoots = WritableRootsFor(SandboxPolicy),
            Shell = Shell
        };
    }

    private static SandboxMode? SandboxModeFor(SandboxPolicy policy) =>
        policy switch
        {
            DangerFullAccessPolicy => Core.SandboxMode.DangerFullAccess,
            ReadOnlyPolicy => Core.SandboxMode.ReadOnly,
            ExternalSandboxPolicy => Core.SandboxMode.DangerFullAccess,
            WorkspaceWritePolicy => Core.SandboxMode.WorkspaceWrite,
            _ => null
        };

    private static NetworkAccess? NetworkAccessFor(SandboxPolicy policy) =>
        policy switch
        {
            DangerFullAccessPolicy => Core.NetworkAccess.Enabled,
            ReadOnlyPolicy => Core.NetworkAccess.Restricted,
            ExternalSandboxPolicy external => external.NetworkAccess,
            WorkspaceWritePolicy workspace => workspace.NetworkAccess ? Core.NetworkAccess.Enabled : Core.NetworkAccess.Restricted,
            _ => null
        };

    private static IReadOnlyList<string>? WritableRootsFor(SandboxPolicy policy) =>
        policy switch
        {
            WorkspaceWritePolicy workspace when workspace.WritableRoots.Count > 0 => workspace.WritableRoots,
            _ => null
        };
}
