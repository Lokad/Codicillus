using Lokad.Codicillus.Core;
using Lokad.Codicillus.Protocol;
using Xunit;

namespace Lokad.Codicillus.Tests;

public sealed class CompactionTests
{
    [Fact]
    public void BuildCompactedHistory_AppendsSummary()
    {
        var initial = new List<ResponseItem>();
        var messages = new List<string> { "first", "second" };
        var summary = "summary";

        var result = Compaction.BuildCompactedHistory(initial, messages, summary);
        var last = Assert.IsType<MessageResponseItem>(result[^1]);

        Assert.Equal("user", last.Role);
        Assert.Equal(summary, ((InputTextContent)last.Content[0]).Text);
    }
}
