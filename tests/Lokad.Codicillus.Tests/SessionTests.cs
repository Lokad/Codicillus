using Lokad.Codicillus.Abstractions;
using Lokad.Codicillus.Core;
using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;
using Xunit;

namespace Lokad.Codicillus.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task PromptCacheKey_UsesConversationId()
    {
        var adapter = new TestModelAdapter(new ModelCapabilities { SupportsPromptCacheKey = true });
        adapter.StreamEvents.Add(new ResponseOutputItemDoneEvent(
            new MessageResponseItem("assistant", [new OutputTextContent("ok")], null)));
        adapter.StreamEvents.Add(new ResponseCompletedEvent(Guid.NewGuid().ToString("N"), null));

        var session = new CodicillusSession(adapter, new TestToolExecutor(), new CodicillusSessionOptions());
        await foreach (var _ in session.RunTurnAsync(new[] { new UserInputText("hi") }, CancellationToken.None))
        {
        }

        Assert.Equal(session.ConversationId.ToString(), adapter.LastPrompt?.PromptCacheKey);
    }

    [Fact]
    public async Task Logger_ReceivesModelAndToolEvents()
    {
        var adapter = new TestModelAdapter(new ModelCapabilities { SupportsPromptCacheKey = false });
        adapter.StreamEvents.Add(new ResponseOutputItemDoneEvent(
            new FunctionCallResponseItem(
                "shell_command",
                "{\"command\":\"echo hi\"}",
                "call-1")));
        adapter.StreamEvents.Add(new ResponseCompletedEvent(Guid.NewGuid().ToString("N"), null));

        var logger = new TestLogger();
        var session = new CodicillusSession(adapter, new TestToolExecutor(), new CodicillusSessionOptions(), logger);
        await foreach (var _ in session.RunTurnAsync(new[] { new UserInputText("hi") }, CancellationToken.None))
        {
        }

        Assert.NotEmpty(logger.ModelEvents);
        Assert.Single(logger.ToolCalls);
        Assert.Single(logger.ToolResults);
    }

    private sealed class TestModelAdapter : IModelAdapter
    {
        public TestModelAdapter(ModelCapabilities capabilities)
        {
            Capabilities = capabilities;
        }

        public ModelCapabilities Capabilities { get; }
        public ModelPrompt? LastPrompt { get; private set; }
        public List<ResponseEvent> StreamEvents { get; } = [];

        public async IAsyncEnumerable<ResponseEvent> StreamAsync(
            ModelPrompt prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastPrompt = prompt;
            var events = StreamEvents.ToList();
            StreamEvents.Clear();
            foreach (var evt in events)
            {
                yield return evt;
            }
            await Task.CompletedTask;
        }

        public Task<IReadOnlyList<ResponseItem>> CompactAsync(ModelPrompt prompt, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ResponseItem>>(Array.Empty<ResponseItem>());
        }
    }

    private sealed class TestToolExecutor : IToolExecutor
    {
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken)
        {
            return Task.FromResult<ToolResult>(new FunctionToolResult
            {
                CallId = call.CallId,
                Output = new FunctionCallOutputPayload { Content = "ok", Success = true }
            });
        }
    }

    private sealed class TestLogger : ICodicillusLogger
    {
        public List<ResponseEvent> ModelEvents { get; } = [];
        public List<ToolCall> ToolCalls { get; } = [];
        public List<ToolResult> ToolResults { get; } = [];

        public void OnModelEvent(ResponseEvent evt) => ModelEvents.Add(evt);

        public void OnToolCall(ToolCall call) => ToolCalls.Add(call);

        public void OnToolResult(ToolResult result) => ToolResults.Add(result);
    }
}
