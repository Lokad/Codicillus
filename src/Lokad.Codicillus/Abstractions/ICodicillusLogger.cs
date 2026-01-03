using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;

namespace Lokad.Codicillus.Abstractions;

public interface ICodicillusLogger
{
    void OnModelEvent(ResponseEvent evt);
    void OnToolCall(ToolCall call);
    void OnToolResult(ToolResult result);
}
