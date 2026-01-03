using System.Runtime.CompilerServices;
using Lokad.Codicillus.Abstractions;
using Lokad.Codicillus.Models;
using Lokad.Codicillus.Protocol;
using Lokad.Codicillus.Tools;

namespace Lokad.Codicillus.Core;

public sealed class CodicillusSession
{
    private readonly IModelAdapter _model;
    private readonly IToolExecutor _tools;
    private readonly ICodicillusLogger? _logger;
    private readonly ContextManager _history = new();
    private readonly TurnContext _turnContext;
    private bool _seededInitialContext;

    public CodicillusSession(
        IModelAdapter model,
        IToolExecutor tools,
        CodicillusSessionOptions options,
        ICodicillusLogger? logger = null)
    {
        _model = model;
        _tools = tools;
        _logger = logger;
        var family = options.ModelFamilyOverride ?? ModelCatalog.FindFamilyForModel(options.Model);
        _turnContext = new TurnContext
        {
            WorkingDirectory = options.WorkingDirectory,
            ApprovalPolicy = options.ApprovalPolicy,
            SandboxPolicy = options.SandboxPolicy,
            Shell = options.Shell,
            DeveloperInstructions = options.DeveloperInstructions,
            UserInstructions = options.UserInstructions,
            ModelFamily = family,
            TruncationPolicy = options.TruncationPolicyOverride ?? family.TruncationPolicy
        };
        ConversationId = Guid.NewGuid();
        ToolSpecs = BuildToolSpecs(options, family);
    }

    public Guid ConversationId { get; }

    public IReadOnlyList<ToolSpec> ToolSpecs { get; }

    public async IAsyncEnumerable<SessionEvent> RunTurnAsync(
        IEnumerable<UserInput> inputs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureInitialContext();
        var userMessage = PromptBuilder.BuildUserInputMessage(inputs);
        _history.RecordItems([userMessage], _turnContext.TruncationPolicy);

        var followUp = true;
        while (followUp && !cancellationToken.IsCancellationRequested)
        {
            followUp = false;
            var prompt = BuildPrompt();
            await foreach (var evt in _model.StreamAsync(prompt, cancellationToken))
            {
                _logger?.OnModelEvent(evt);
                yield return new ModelSessionEvent(evt);

                if (evt is not ResponseOutputItemDoneEvent outputEvent)
                {
                    continue;
                }

                var item = outputEvent.Item;
                _history.RecordItems([item], _turnContext.TruncationPolicy);
                var toolCall = ToolRouter.TryBuildToolCall(item);
                if (toolCall is null)
                {
                    continue;
                }

                _logger?.OnToolCall(toolCall);
                yield return new ToolCallSessionEvent(toolCall);
                var toolResult = await _tools.ExecuteAsync(toolCall, cancellationToken);
                _logger?.OnToolResult(toolResult);
                yield return new ToolResultSessionEvent(toolResult);

                var responseInput = toolResult.ToResponseInputItem();
                var responseItem = ToResponseItem(responseInput);
                _history.RecordItems([responseItem], _turnContext.TruncationPolicy);
                followUp = true;
            }
        }
    }

    public async Task CompactAsync(CancellationToken cancellationToken)
    {
        EnsureInitialContext();
        if (_model.Capabilities.SupportsRemoteCompaction)
        {
            var prompt = BuildPrompt(overrideTools: []);
            var compacted = await _model.CompactAsync(prompt, cancellationToken);
            _history.Replace(compacted);
            return;
        }

        var summaryPrompt = new MessageResponseItem(
            "user",
            [new InputTextContent(Compaction.SummarizationPrompt)],
            null);
        _history.RecordItems([summaryPrompt], _turnContext.TruncationPolicy);

        var promptForSummary = BuildPrompt(overrideTools: []);
        string? summaryText = null;
        await foreach (var evt in _model.StreamAsync(promptForSummary, cancellationToken))
        {
            _logger?.OnModelEvent(evt);
            if (evt is ResponseOutputItemDoneEvent outputEvent &&
                outputEvent.Item is MessageResponseItem message &&
                message.Role == "assistant")
            {
                summaryText = Compaction.ContentItemsToText(message.Content);
            }
        }

        var historySnapshot = _history.GetHistory();
        var userMessages = Compaction.CollectUserMessages(historySnapshot);
        var summarySuffix = summaryText ?? string.Empty;
        var summary = $"{Compaction.SummaryPrefix}\n{summarySuffix}";
        var rebuilt = Compaction.BuildCompactedHistory(
            PromptBuilder.BuildInitialContext(_turnContext),
            userMessages,
            summary);
        _history.Replace(rebuilt);
    }

    private void EnsureInitialContext()
    {
        if (_seededInitialContext)
        {
            return;
        }

        var items = PromptBuilder.BuildInitialContext(_turnContext);
        _history.RecordItems(items, _turnContext.TruncationPolicy);
        _seededInitialContext = true;
    }

    private ModelPrompt BuildPrompt(IReadOnlyList<ToolSpec>? overrideTools = null)
    {
        var input = _history.GetHistoryForPrompt();
        var instructions = _turnContext.ModelFamily.BaseInstructions;
        return new ModelPrompt
        {
            Instructions = instructions,
            Input = input,
            Tools = overrideTools ?? ToolSpecs,
            ParallelToolCalls = _turnContext.ModelFamily.SupportsParallelToolCalls,
            PromptCacheKey = _model.Capabilities.SupportsPromptCacheKey ? ConversationId.ToString() : null
        };
    }

    private static IReadOnlyList<ToolSpec> BuildToolSpecs(CodicillusSessionOptions options, ModelFamily family)
    {
        var tools = new List<ToolSpec>();
        if (options.EnableShellTool)
        {
            tools.Add(BuiltInTools.CreateShellTool());
            tools.Add(BuiltInTools.CreateShellCommandTool());
        }
        if (options.EnableApplyPatchTool)
        {
            tools.Add(family.ApplyPatchToolType == Models.ApplyPatchToolType.Function
                ? BuiltInTools.CreateApplyPatchJsonTool()
                : BuiltInTools.CreateApplyPatchFreeformTool());
        }
        if (options.EnableViewImageTool)
        {
            tools.Add(BuiltInTools.CreateViewImageTool());
        }
        return tools;
    }

    private static ResponseItem ToResponseItem(ResponseInputItem input) =>
        input switch
        {
            ResponseInputMessageItem message =>
                new MessageResponseItem(message.Role, message.Content, null),
            ResponseInputFunctionCallOutputItem functionOutput =>
                new FunctionCallOutputResponseItem(functionOutput.CallId, functionOutput.Output),
            ResponseInputCustomToolCallOutputItem customOutput =>
                new CustomToolCallOutputResponseItem(customOutput.CallId, customOutput.Output),
            _ => new OtherResponseItem(default)
        };
}
