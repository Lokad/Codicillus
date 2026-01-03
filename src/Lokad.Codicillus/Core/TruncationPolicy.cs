namespace Lokad.Codicillus.Core;

public enum TruncationPolicyKind
{
    Bytes,
    Tokens
}

public readonly record struct TruncationPolicy(TruncationPolicyKind Kind, int Limit)
{
    public static TruncationPolicy Bytes(int limit) => new(TruncationPolicyKind.Bytes, limit);
    public static TruncationPolicy Tokens(int limit) => new(TruncationPolicyKind.Tokens, limit);

    public TruncationPolicy Multiply(double multiplier)
    {
        var scaled = (int)Math.Ceiling(Limit * multiplier);
        return new TruncationPolicy(Kind, scaled);
    }

    public int TokenBudget =>
        Kind == TruncationPolicyKind.Tokens
            ? Limit
            : Truncation.ApproxTokensFromByteCount(Limit);

    public int ByteBudget =>
        Kind == TruncationPolicyKind.Bytes
            ? Limit
            : Truncation.ApproxBytesForTokens(Limit);
}
