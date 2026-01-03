using Lokad.Codicillus.Core;
using Xunit;

namespace Lokad.Codicillus.Tests;

public sealed class TruncationTests
{
    [Fact]
    public void FormattedTruncateText_IncludesLineCount()
    {
        var text = "line1\nline2\nline3";
        var result = Truncation.FormattedTruncateText(text, TruncationPolicy.Bytes(5));

        Assert.StartsWith("Total output lines: 3", result);
        Assert.Contains("chars truncated", result);
    }

    [Fact]
    public void TruncateText_TokenMarkerAppears()
    {
        var text = "this is a long output that should be truncated";
        var result = Truncation.TruncateText(text, TruncationPolicy.Tokens(2));

        Assert.Contains("tokens truncated", result);
    }
}
