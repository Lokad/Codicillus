using System.Text.Json;
using Lokad.Codicillus.Tools;
using Xunit;

namespace Lokad.Codicillus.Tests;

public sealed class ToolSchemaTests
{
    [Fact]
    public void ShellToolSchema_ContainsExpectedFields()
    {
        var tool = BuiltInTools.CreateShellTool();
        var json = JsonSerializer.Serialize(tool);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("function", root.GetProperty("type").GetString());
        Assert.Equal("shell", root.GetProperty("name").GetString());
        Assert.True(root.TryGetProperty("parameters", out _));
    }

    [Fact]
    public void ApplyPatchFreeformToolSchema_UsesGrammar()
    {
        var tool = BuiltInTools.CreateApplyPatchFreeformTool();
        var json = JsonSerializer.Serialize(tool);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("custom", root.GetProperty("type").GetString());
        Assert.Equal("apply_patch", root.GetProperty("name").GetString());
        Assert.Equal("grammar", root.GetProperty("format").GetProperty("type").GetString());
    }
}
