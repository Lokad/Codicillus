using System.Runtime.InteropServices;
using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Tools;

public static class BuiltInTools
{
    private const string ApplyPatchLarkGrammar = """
start: begin_patch hunk+ end_patch
begin_patch: "*** Begin Patch" LF
end_patch: "*** End Patch" LF?

hunk: add_hunk | delete_hunk | update_hunk
add_hunk: "*** Add File: " filename LF add_line+
delete_hunk: "*** Delete File: " filename LF
update_hunk: "*** Update File: " filename LF change_move? change?

filename: /(.+)/
add_line: "+" /(.*)/ LF -> line

change_move: "*** Move to: " filename LF
change: (change_context | change_line)+ eof_line?
change_context: ("@@" | "@@ " /(.+)/) LF
change_line: ("+" | "-" | " ") /(.*)/ LF
eof_line: "*** End of File" LF

%import common.LF
""";

    private const string ApplyPatchJsonDescription = """
Use the `apply_patch` tool to edit files.
Your patch language is a stripped-down, file-oriented diff format designed to be easy to parse and safe to apply.
*** Begin Patch
[ one or more file sections ]
*** End Patch

Each operation starts with one of:
*** Add File: <path>
*** Delete File: <path>
*** Update File: <path>
""";

    public static ToolSpec CreateShellTool()
    {
        var properties = new Dictionary<string, JsonSchema>
        {
            ["command"] = new JsonSchemaArray(new JsonSchemaString(), "The command to execute"),
            ["workdir"] = new JsonSchemaString("The working directory to execute the command in"),
            ["timeout_ms"] = new JsonSchemaNumber("The timeout for the command in milliseconds"),
            ["sandbox_permissions"] = new JsonSchemaString(
                "Sandbox permissions for the command. Set to \"require_escalated\" to request running without sandbox restrictions; defaults to \"use_default\"."),
            ["justification"] = new JsonSchemaString(
                "Only set if sandbox_permissions is \"require_escalated\". 1-sentence explanation of why we want to run this command.")
        };

        var description = OperatingSystem.IsWindows()
            ? """
Runs a Powershell command (Windows) and returns its output. Arguments to `shell` will be passed to CreateProcessW(). Most commands should be prefixed with ["powershell.exe", "-Command"].

Examples of valid command strings:

- ls -a (show hidden): ["powershell.exe", "-Command", "Get-ChildItem -Force"]
- recursive find by name: ["powershell.exe", "-Command", "Get-ChildItem -Recurse -Filter *.py"]
- recursive grep: ["powershell.exe", "-Command", "Get-ChildItem -Path C:\\myrepo -Recurse | Select-String -Pattern 'TODO' -CaseSensitive"]
- ps aux | grep python: ["powershell.exe", "-Command", "Get-Process | Where-Object { $_.ProcessName -like '*python*' }"]
- setting an env var: ["powershell.exe", "-Command", "$env:FOO='bar'; echo $env:FOO"]
- running an inline Python script: ["powershell.exe", "-Command", "@'\\nprint('Hello, world!')\\n'@ | python -"]
"""
            : """
Runs a shell command and returns its output.
- The arguments to `shell` will be passed to execvp(). Most terminal commands should be prefixed with ["bash", "-lc"].
- Always set the `workdir` param when using the shell function. Do not use `cd` unless absolutely necessary.
""";

        return new FunctionToolSpec(
            "shell",
            description,
            false,
            new JsonSchemaObject(properties, new[] { "command" }, new AdditionalPropertiesBoolean(false)));
    }

    public static ToolSpec CreateShellCommandTool()
    {
        var properties = new Dictionary<string, JsonSchema>
        {
            ["command"] = new JsonSchemaString("The shell script to execute in the user's default shell"),
            ["workdir"] = new JsonSchemaString("The working directory to execute the command in"),
            ["login"] = new JsonSchemaBoolean("Whether to run the shell with login shell semantics. Defaults to true."),
            ["timeout_ms"] = new JsonSchemaNumber("The timeout for the command in milliseconds"),
            ["sandbox_permissions"] = new JsonSchemaString(
                "Sandbox permissions for the command. Set to \"require_escalated\" to request running without sandbox restrictions; defaults to \"use_default\"."),
            ["justification"] = new JsonSchemaString(
                "Only set if sandbox_permissions is \"require_escalated\". 1-sentence explanation of why we want to run this command.")
        };

        var description = OperatingSystem.IsWindows()
            ? """
Runs a Powershell command (Windows) and returns its output.

Examples of valid command strings:

- ls -a (show hidden): "Get-ChildItem -Force"
- recursive find by name: "Get-ChildItem -Recurse -Filter *.py"
- recursive grep: "Get-ChildItem -Path C:\\myrepo -Recurse | Select-String -Pattern 'TODO' -CaseSensitive"
- ps aux | grep python: "Get-Process | Where-Object { $_.ProcessName -like '*python*' }"
- setting an env var: "$env:FOO='bar'; echo $env:FOO"
- running an inline Python script: "@'\\nprint('Hello, world!')\\n'@ | python -"
"""
            : """
Runs a shell command and returns its output.
- Always set the `workdir` param when using the shell_command function. Do not use `cd` unless absolutely necessary.
""";

        return new FunctionToolSpec(
            "shell_command",
            description,
            false,
            new JsonSchemaObject(properties, new[] { "command" }, new AdditionalPropertiesBoolean(false)));
    }

    public static ToolSpec CreateViewImageTool()
    {
        var properties = new Dictionary<string, JsonSchema>
        {
            ["path"] = new JsonSchemaString("Local filesystem path to an image file")
        };

        return new FunctionToolSpec(
            "view_image",
            "Attach a local image (by filesystem path) to the conversation context for this turn.",
            false,
            new JsonSchemaObject(properties, new[] { "path" }, new AdditionalPropertiesBoolean(false)));
    }

    public static ToolSpec CreateApplyPatchFreeformTool()
    {
        return new CustomToolSpec(
            "apply_patch",
            "Use the `apply_patch` tool to edit files. This is a FREEFORM tool, so do not wrap the patch in JSON.",
            new FreeformToolFormat("grammar", "lark", ApplyPatchLarkGrammar));
    }

    public static ToolSpec CreateApplyPatchJsonTool()
    {
        var properties = new Dictionary<string, JsonSchema>
        {
            ["input"] = new JsonSchemaString("The entire contents of the apply_patch command")
        };

        return new FunctionToolSpec(
            "apply_patch",
            ApplyPatchJsonDescription,
            false,
            new JsonSchemaObject(properties, new[] { "input" }, new AdditionalPropertiesBoolean(false)));
    }
}
