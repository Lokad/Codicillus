using System.Text.Json;
using Lokad.Codicillus.Tools;
using Xunit;

namespace Lokad.Codicillus.Tests;

public sealed class ApplyPatchTests
{
    [Fact]
    public async Task ApplyPatchAddsFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            var patch = string.Join("\n", new[]
            {
                "*** Begin Patch",
                "*** Add File: hello.txt",
                "+line1",
                "+line2",
                "*** End Patch"
            });

            var result = await RunPatchAsync(tempDir, patch);
            Assert.True(result.Output.Success);
            var content = await File.ReadAllTextAsync(Path.Combine(tempDir, "hello.txt"));
            Assert.Equal("line1\nline2", Normalize(content));
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    [Fact]
    public async Task ApplyPatchUpdatesFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "hello.txt");
            await File.WriteAllTextAsync(path, "line1\nline2\n");

            var patch = string.Join("\n", new[]
            {
                "*** Begin Patch",
                "*** Update File: hello.txt",
                "@@",
                "-line1",
                "+line1a",
                " line2",
                "*** End Patch"
            });

            var result = await RunPatchAsync(tempDir, patch);
            Assert.True(result.Output.Success);
            var content = await File.ReadAllTextAsync(path);
            Assert.Equal("line1a\nline2\n", Normalize(content));
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    [Fact]
    public async Task ApplyPatchDeletesFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "hello.txt");
            await File.WriteAllTextAsync(path, "line1");

            var patch = string.Join("\n", new[]
            {
                "*** Begin Patch",
                "*** Delete File: hello.txt",
                "*** End Patch"
            });

            var result = await RunPatchAsync(tempDir, patch);
            Assert.True(result.Output.Success);
            Assert.False(File.Exists(path));
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    private static async Task<FunctionToolResult> RunPatchAsync(string baseDir, string patch)
    {
        var executor = new LocalToolExecutor(baseDir);
        var args = JsonSerializer.Serialize(new ApplyPatchToolArgs { Input = patch });
        var call = new FunctionToolCall
        {
            CallId = Guid.NewGuid().ToString("N"),
            Name = "apply_patch",
            ArgumentsJson = args
        };
        var result = await executor.ExecuteAsync(call, CancellationToken.None);
        return Assert.IsType<FunctionToolResult>(result);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "codicillus-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
