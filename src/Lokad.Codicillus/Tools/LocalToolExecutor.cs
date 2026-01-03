using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Lokad.Codicillus.Abstractions;
using Lokad.Codicillus.Protocol;

namespace Lokad.Codicillus.Tools;

public sealed class LocalToolExecutor : IToolExecutor
{
    private readonly string _baseDirectory;

    public LocalToolExecutor(string? baseDirectory = null)
    {
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Directory.GetCurrentDirectory()
            : baseDirectory;
    }

    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
    {
        return call switch
        {
            FunctionToolCall functionCall => await ExecuteFunctionToolAsync(functionCall, cancellationToken),
            CustomToolCall customToolCall => await ExecuteCustomToolAsync(customToolCall, cancellationToken),
            _ => throw new InvalidOperationException("Unknown tool call type.")
        };
    }

    private async Task<ToolResult> ExecuteFunctionToolAsync(FunctionToolCall call, CancellationToken cancellationToken)
    {
        return call.Name switch
        {
            "shell" => await ExecuteShellAsync(call.CallId, call.ArgumentsJson, cancellationToken),
            "shell_command" => await ExecuteShellCommandAsync(call.CallId, call.ArgumentsJson, cancellationToken),
            "apply_patch" => await ExecuteApplyPatchAsync(call.CallId, call.ArgumentsJson),
            "view_image" => new FunctionToolResult
            {
                CallId = call.CallId,
                Output = new FunctionCallOutputPayload
                {
                    Content = "view_image is not implemented by the local executor",
                    Success = false
                }
            },
            _ => new FunctionToolResult
            {
                CallId = call.CallId,
                Output = new FunctionCallOutputPayload
                {
                    Content = $"Unknown tool: {call.Name}",
                    Success = false
                }
            }
        };
    }

    private Task<ToolResult> ExecuteCustomToolAsync(CustomToolCall call, CancellationToken cancellationToken)
    {
        if (call.Name == "apply_patch")
        {
            var result = ApplyPatchEngine.Apply(call.Input, _baseDirectory);
            return Task.FromResult<ToolResult>(new CustomToolResult
            {
                CallId = call.CallId,
                Output = result.Message
            });
        }

        return Task.FromResult<ToolResult>(new CustomToolResult
        {
            CallId = call.CallId,
            Output = $"Custom tool '{call.Name}' not implemented"
        });
    }

    private Task<ToolResult> ExecuteApplyPatchAsync(string callId, string argsJson)
    {
        ApplyPatchToolArgs? args;
        try
        {
            args = JsonSerializer.Deserialize<ApplyPatchToolArgs>(argsJson);
        }
        catch (JsonException ex)
        {
            return Task.FromResult<ToolResult>(new FunctionToolResult
            {
                CallId = callId,
                Output = new FunctionCallOutputPayload
                {
                    Content = $"apply_patch invalid arguments: {ex.Message}",
                    Success = false
                }
            });
        }

        if (args is null || string.IsNullOrWhiteSpace(args.Input))
        {
            return Task.FromResult<ToolResult>(new FunctionToolResult
            {
                CallId = callId,
                Output = new FunctionCallOutputPayload
                {
                    Content = "apply_patch missing input",
                    Success = false
                }
            });
        }

        var result = ApplyPatchEngine.Apply(args.Input, _baseDirectory);
        return Task.FromResult<ToolResult>(new FunctionToolResult
        {
            CallId = callId,
            Output = new FunctionCallOutputPayload
            {
                Content = result.Message,
                Success = result.Success
            }
        });
    }

    private async Task<ToolResult> ExecuteShellAsync(string callId, string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<ShellToolCallParams>(argsJson)
                   ?? new ShellToolCallParams();
        if (args.Command.Count == 0)
        {
            return new FunctionToolResult
            {
                CallId = callId,
                Output = new FunctionCallOutputPayload { Content = "shell: missing command", Success = false }
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = args.Command[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        for (var i = 1; i < args.Command.Count; i++)
        {
            startInfo.ArgumentList.Add(args.Command[i]);
        }
        if (!string.IsNullOrWhiteSpace(args.Workdir))
        {
            startInfo.WorkingDirectory = args.Workdir;
        }

        return await RunProcessAsync(callId, startInfo, args.TimeoutMs, cancellationToken);
    }

    private async Task<ToolResult> ExecuteShellCommandAsync(string callId, string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<ShellCommandToolCallParams>(argsJson)
                   ?? new ShellCommandToolCallParams();
        if (string.IsNullOrWhiteSpace(args.Command))
        {
            return new FunctionToolResult
            {
                CallId = callId,
                Output = new FunctionCallOutputPayload { Content = "shell_command: missing command", Success = false }
            };
        }

        var startInfo = BuildDefaultShellStartInfo(args.Command);
        if (!string.IsNullOrWhiteSpace(args.Workdir))
        {
            startInfo.WorkingDirectory = args.Workdir;
        }

        return await RunProcessAsync(callId, startInfo, args.TimeoutMs, cancellationToken);
    }

    private static ProcessStartInfo BuildDefaultShellStartInfo(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                ArgumentList = { "-Command", command }
            };
        }

        return new ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { "-lc", command }
        };
    }

    private static async Task<ToolResult> RunProcessAsync(
        string callId,
        ProcessStartInfo startInfo,
        long? timeoutMs,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        var started = process.Start();
        if (!started)
        {
            return new FunctionToolResult
            {
                CallId = callId,
                Output = new FunctionCallOutputPayload { Content = "Process failed to start", Success = false }
            };
        }

        var timeout = timeoutMs.HasValue && timeoutMs > 0
            ? TimeSpan.FromMilliseconds(timeoutMs.Value)
            : Timeout.InfiniteTimeSpan;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCts.CancelAfter(timeout);
        }

        var stopwatch = Stopwatch.StartNew();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill failures.
            }
        }
        stopwatch.Stop();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}{stderr}";

        var payload = new ShellExecOutput
        {
            Output = output ?? string.Empty,
            Metadata = new ShellExecMetadata
            {
                ExitCode = process.HasExited ? process.ExitCode : -1,
                DurationSeconds = stopwatch.Elapsed.TotalSeconds
            }
        };

        return new FunctionToolResult
        {
            CallId = callId,
            Output = new FunctionCallOutputPayload
            {
                Content = JsonSerializer.Serialize(payload),
                Success = payload.Metadata.ExitCode == 0
            }
        };
    }

    private sealed record ShellExecOutput
    {
        public string Output { get; init; } = string.Empty;
        public ShellExecMetadata Metadata { get; init; } = new();
    }

    private sealed record ShellExecMetadata
    {
        public int ExitCode { get; init; }
        public double DurationSeconds { get; init; }
    }
}
