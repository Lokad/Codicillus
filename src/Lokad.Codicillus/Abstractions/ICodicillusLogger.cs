using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;

namespace Lokad.Codicillus.Abstractions;

/// <summary>
/// Optional logging hook for model and tool events emitted by a Codicillus session.
/// Hosts can implement this interface to forward events into their own logging system.
/// </summary>
public interface ICodicillusLogger
{
    void OnModelEvent(ResponseEvent evt);
    void OnToolCall(ToolCall call);
    void OnToolResult(ToolResult result);
}
