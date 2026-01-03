using System.Text;
using System.Text.Json.Serialization;

namespace Lokad.Codicillus.Core;

public enum AskForApproval
{
    Untrusted,
    OnFailure,
    OnRequest,
    Never
}

public enum NetworkAccess
{
    Restricted,
    Enabled
}

public enum SandboxMode
{
    DangerFullAccess,
    ReadOnly,
    WorkspaceWrite
}

public abstract record SandboxPolicy;

public sealed record DangerFullAccessPolicy : SandboxPolicy;

public sealed record ReadOnlyPolicy : SandboxPolicy;

public sealed record ExternalSandboxPolicy(NetworkAccess NetworkAccess) : SandboxPolicy;

public sealed record WorkspaceWritePolicy(
    IReadOnlyList<string> WritableRoots,
    bool NetworkAccess
) : SandboxPolicy;

public enum ShellType
{
    Bash,
    PowerShell
}

public sealed record ShellInfo(
    ShellType ShellType,
    string? ShellPath = null
);

public sealed record EnvironmentContext
{
    public string? Cwd { get; init; }
    public AskForApproval? ApprovalPolicy { get; init; }
    public SandboxMode? SandboxMode { get; init; }
    public NetworkAccess? NetworkAccess { get; init; }
    public IReadOnlyList<string>? WritableRoots { get; init; }
    public ShellInfo Shell { get; init; } = new(ShellType.Bash);

    public string SerializeToXml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<environment_context>");
        if (!string.IsNullOrWhiteSpace(Cwd))
        {
            sb.AppendLine($"  <cwd>{Cwd}</cwd>");
        }
        if (ApprovalPolicy is not null)
        {
            sb.AppendLine($"  <approval_policy>{ToKebabCase(ApprovalPolicy.Value)}</approval_policy>");
        }
        if (SandboxMode is not null)
        {
            sb.AppendLine($"  <sandbox_mode>{ToKebabCase(SandboxMode.Value)}</sandbox_mode>");
        }
        if (NetworkAccess is not null)
        {
            sb.AppendLine($"  <network_access>{ToKebabCase(NetworkAccess.Value)}</network_access>");
        }
        if (WritableRoots is not null && WritableRoots.Count > 0)
        {
            sb.AppendLine("  <writable_roots>");
            foreach (var root in WritableRoots)
            {
                sb.AppendLine($"    <root>{root}</root>");
            }
            sb.AppendLine("  </writable_roots>");
        }
        sb.AppendLine($"  <shell>{ToShellName(Shell.ShellType)}</shell>");
        sb.AppendLine("</environment_context>");
        return sb.ToString().TrimEnd();
    }

    private static string ToShellName(ShellType shellType) =>
        shellType == ShellType.PowerShell ? "powershell" : "bash";

    private static string ToKebabCase(Enum value)
    {
        var name = value.ToString();
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch) && i > 0)
            {
                sb.Append('-');
            }
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }
}
