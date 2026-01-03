namespace Lokad.Codicillus.Protocol;

public abstract record ResponseEvent;

public sealed record ResponseCreatedEvent : ResponseEvent;

public sealed record ResponseOutputItemDoneEvent(ResponseItem Item) : ResponseEvent;

public sealed record ResponseOutputItemAddedEvent(ResponseItem Item) : ResponseEvent;

public sealed record ResponseCompletedEvent(
    string ResponseId,
    TokenUsage? TokenUsage
) : ResponseEvent;

public sealed record ResponseOutputTextDeltaEvent(string Delta) : ResponseEvent;

public sealed record ResponseReasoningSummaryDeltaEvent(
    string Delta,
    long SummaryIndex
) : ResponseEvent;

public sealed record ResponseReasoningContentDeltaEvent(
    string Delta,
    long ContentIndex
) : ResponseEvent;

public sealed record ResponseReasoningSummaryPartAddedEvent(long SummaryIndex) : ResponseEvent;

public sealed record ResponseRateLimitsEvent(RateLimitSnapshot Snapshot) : ResponseEvent;

public sealed record ResponseModelsEtagEvent(string Etag) : ResponseEvent;
