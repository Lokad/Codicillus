using Lokad.Codicillus.Core;
using Xunit;

namespace Lokad.Codicillus.Tests;

public sealed class EnvironmentContextTests
{
    [Fact]
    public void SerializeToXml_EmitsExpectedTags()
    {
        var context = new EnvironmentContext
        {
            Cwd = "C:\\repo",
            ApprovalPolicy = AskForApproval.Never,
            SandboxMode = SandboxMode.DangerFullAccess,
            NetworkAccess = NetworkAccess.Enabled,
            Shell = new ShellInfo(ShellType.PowerShell)
        };

        var expected = string.Join(Environment.NewLine,
        [
            "<environment_context>",
            "  <cwd>C:\\repo</cwd>",
            "  <approval_policy>never</approval_policy>",
            "  <sandbox_mode>danger-full-access</sandbox_mode>",
            "  <network_access>enabled</network_access>",
            "  <shell>powershell</shell>",
            "</environment_context>"
        ]);

        Assert.Equal(expected, context.SerializeToXml());
    }
}
