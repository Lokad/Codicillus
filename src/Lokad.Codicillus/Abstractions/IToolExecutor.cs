using Lokad.Codicillus.Tools;

namespace Lokad.Codicillus.Abstractions;

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken);
}
