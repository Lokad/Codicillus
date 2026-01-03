using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;

namespace Lokad.Codicillus.Core;

public abstract record SessionEvent;

public sealed record ModelSessionEvent(ResponseEvent Event) : SessionEvent;

public sealed record ToolCallSessionEvent(ToolCall Call) : SessionEvent;

public sealed record ToolResultSessionEvent(ToolResult Result) : SessionEvent;

public sealed record ContextCompactedSessionEvent : SessionEvent;

public sealed record WarningSessionEvent(string Message) : SessionEvent;
