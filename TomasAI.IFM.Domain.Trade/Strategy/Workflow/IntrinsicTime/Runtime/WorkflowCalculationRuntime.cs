using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>The local calculation boundary has no persistence, provider, actor or financial-commit capability.</summary>
public sealed class WorkflowCalculationRuntime(WorkflowDecisionExecutor executor, TimeProvider? timeProvider = null,
    IPortfolioEntryDecisionCalculator? portfolioCalculator = null, WorkflowDecisionSession? session = null,
    WorkflowSelectionConfigurationCache? selectionConfiguration = null, WorkflowCompositionInputCache? compositionConfiguration = null,
    WorkflowRuntimePreparation? preparation = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly RegimeOperator regime = new();
    readonly AssessmentOperator assessment = new();
    readonly SelectionOperator selection = new();
    readonly CompositionOperator composition = new();
    readonly RiskOperator risk = new();
    readonly WorkflowDecisionSession lifetime = session ?? new();
    public Guid SessionId => lifetime.Id;
    public bool IsPrepared => preparation?.IsReady ?? true;
    readonly object attemptsGate = new();
    readonly Dictionary<(Guid Workflow, StrategyWorkflowStage Stage), Attempt> attempts = [];
    readonly System.Collections.Concurrent.ConcurrentDictionary<(Guid Workflow, StrategyWorkflowStage Stage, long Revision), StrategyStageResultEnvelope> results = new();
    sealed record Attempt(long Revision, DateTime Until, Lazy<Task<object>> Work);

    internal void RecordResult(IntrinsicTimeStrategyWorkflowView view, StrategyStageResultEnvelope result) =>
        results.TryAdd((view.WorkflowId.Value, view.CurrentStage, view.WorkflowRevision), result);

    internal bool MatchesResult(IntrinsicTimeStrategyWorkflowView view, StrategyStageResultEnvelope result) =>
        results.TryGetValue((view.WorkflowId.Value, view.CurrentStage, view.WorkflowRevision), out var produced) && produced.HasSameContent(result);

    internal bool MatchesApproval(IntrinsicTimeStrategyWorkflowView view, PortfolioEntryApproval approval)
    {
        lock (attemptsGate)
            return attempts.TryGetValue((view.WorkflowId.Value, StrategyWorkflowStage.RiskManagement), out var attempt)
                && attempt.Revision == view.WorkflowRevision && attempt.Work.IsValueCreated && attempt.Work.Value.IsCompletedSuccessfully
                && attempt.Work.Value.Result is PortfolioEntryApproval produced && ReferenceEquals(produced, approval);
    }

    internal ExecuteTradeSelectionPipelineCommand CreateSelectionDispatch(IntrinsicTimeStrategyWorkflowView view, Guid sourceEventId) =>
        TradeSelectionDispatch.Create(view, sourceEventId, selectionConfiguration?.TryGetCompiled(view.SelectionBinding!)
            ?? throw new InvalidOperationException("TS.CONFIG.COMPILED_NOT_READY"));

    public ValueTask<RegimeDiscoveryResult> RegimeAsync(ExecuteRegimeDiscoveryPipelineCommand command, CancellationToken token = default) =>
        RunAsync(regime, new RegimeDiscoveryCalculationInput { CalculationPolicy = command.CalculationPolicy,
            ResultId = command.CommandId, WorkflowId = command.WorkflowId, EntityId = command.WorkflowEntityId,
            TriggerEventId = command.TriggerEvent.Id, TriggerEvent = command.TriggerEvent, ParameterSet = command.ParameterSet,
            Snapshot = command.Snapshot, ProducedAtUtc = clock.GetUtcNow().UtcDateTime }, command.WorkflowView, token);

    public ValueTask<MarketConditionAssessmentResult> AssessmentAsync(ExecuteMarketConditionAssessmentCommand command,
        MarketConditionAssessmentSnapshot snapshot, CancellationToken token = default) =>
        RunAsync(assessment, new AssessmentInput(command, snapshot), command.WorkflowView, token);

    public ValueTask<TradeSelectionResult> SelectionAsync(ExecuteTradeSelectionPipelineCommand command, CancellationToken token = default) =>
        RunAsync(selection, new SelectionInput(command, selectionConfiguration?.TryGetCompiled(command.SelectionBinding)
            ?? throw new InvalidOperationException("TS.CONFIG.COMPILED_NOT_READY")), command.WorkflowView, token);

    public ValueTask<OrderCompositionResult> CompositionAsync(ExecuteOrderCompositionPipelineCommand command, CancellationToken token = default) =>
        RunAsync(composition, new CompositionInput(command, compositionConfiguration?.TryGetConfiguration(command.CompositionBinding.Selected.CandidateHash)
            ?? throw new InvalidOperationException("OC.CONFIG.COMPILED_NOT_READY")), command.WorkflowView, token);

    internal ExecuteOrderCompositionPipelineCommand CreateCompositionDispatch(IntrinsicTimeStrategyWorkflowView view,
        Application.MarketData.Pricing.CompositionPreparation preparation, DateTime now) =>
        CompositionDispatch.Create(view, preparation, now, compositionConfiguration?.TryGetConfiguration(
            view.TradeSelection.Result!.SelectionResult!.SelectedCandidate!.CandidateHash)
            ?? throw new InvalidOperationException("OC.CONFIG.COMPILED_NOT_READY"));

    public ValueTask<RiskAssessmentResult> RiskAsync(ExecuteRiskManagementPipelineCommand command,
        IntrinsicTimeStrategyWorkflowView view, CancellationToken token = default) => RunAsync(risk, command, view, token);

    public async ValueTask<PortfolioEntryApproval> PortfolioAsync(EvaluatePortfolioOrderCompositionCommand request,
        PortfolioDecisionFinancialState financial, IntrinsicTimeStrategyWorkflowView view, CancellationToken token = default)
    {
        var calculator = portfolioCalculator ?? throw new InvalidOperationException("RM.PORTFOLIO.CALCULATOR_NOT_READY");
        return await RunAsync(new PortfolioOperator(calculator, clock, lifetime), new PortfolioInput(request, financial), view, token).ConfigureAwait(false);
    }

    public static DateTime EffectiveDeadline(IntrinsicTimeStrategyWorkflowView view) =>
        view.DecisionRuntimeVersion > 0 ? Deadline(view) : view.ExpiresAtUtc;

    public static IntrinsicTimeStrategyWorkflowView StampActiveDeadline(IntrinsicTimeStrategyWorkflowView view)
    {
        if (view.DecisionRuntimeVersion == 0 || view.Status != WorkflowStrategyMachineStatus.Started
            || CurrentStage(view).ProcessingStatus != StrategyActorProcessingStatus.Processing) return view;
        var stage = CurrentStage(view) with { ExpiresAtUtc = Deadline(view) };
        return view.CurrentStage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => view with { RegimeDiscovery = stage },
            StrategyWorkflowStage.MarketCondition => view with { MarketCondition = stage },
            StrategyWorkflowStage.TradeSelection => view with { TradeSelection = stage },
            StrategyWorkflowStage.OrderComposition => view with { OrderComposition = stage },
            StrategyWorkflowStage.RiskManagement => view with { RiskManagement = stage },
            _ => view
        };
    }

    async ValueTask<TOutput> RunAsync<TInput, TOutput>(IWorkflowDecisionOperator<TInput, TOutput> operation,
        TInput input, IntrinsicTimeStrategyWorkflowView view, CancellationToken token)
    {
        preparation?.RequireReady();
        Attempt attempt;
        lock (attemptsGate)
        {
            var key = (view.WorkflowId.Value, view.CurrentStage);
            if (!attempts.TryGetValue(key, out attempt!))
            {
                if (attempts.Count >= 512)
                {
                    var expired = attempts.Where(value => value.Value.Until <= clock.GetUtcNow().UtcDateTime).Select(value => value.Key).ToArray();
                    foreach (var old in expired)
                    {
                        attempts.Remove(old);
                        foreach (var proof in results.Keys.Where(value => value.Workflow == old.Workflow)) results.TryRemove(proof, out _);
                    }
                }
                if (attempts.Count >= 1024) throw new WorkflowDecisionException(DecisionExecutionStatus.Overloaded);
                attempt = new(view.WorkflowRevision, view.ExpiresAtUtc,
                    new Lazy<Task<object>>(async () => (object)(await CalculateOnceAsync(operation, input, view, token).ConfigureAwait(false))!,
                        LazyThreadSafetyMode.ExecutionAndPublication));
                attempts.Add(key, attempt);
            }
            if (attempt.Revision != view.WorkflowRevision) throw new InvalidOperationException("WF.DECISION.INPUT_REVISION_CHANGED");
        }
        return (TOutput)await attempt.Work.Value.ConfigureAwait(false);
    }

    async ValueTask<TOutput> CalculateOnceAsync<TInput, TOutput>(IWorkflowDecisionOperator<TInput, TOutput> operation,
        TInput input, IntrinsicTimeStrategyWorkflowView view, CancellationToken token)
    {
        var readyAt = CurrentStage(view).StartedAtUtc ?? view.UpdatedAtUtc;
        var preparationMilliseconds = (clock.GetUtcNow().UtcDateTime - readyAt).TotalMilliseconds;
        var result = await executor.ExecuteAsync(operation, input, readyAt, EffectiveDeadline(view), token).ConfigureAwait(false);
        WorkflowDecisionTelemetry.Record(view, result, preparationMilliseconds);
        if (result.Status != DecisionExecutionStatus.Completed) throw new WorkflowDecisionException(result.Status, result.Error);
        return result.Value!;
    }

    public static DateTime Deadline(IntrinsicTimeStrategyWorkflowView view)
    {
        var stage = CurrentStage(view);
        var deadline = (stage.StartedAtUtc ?? view.UpdatedAtUtc).Add(WorkflowDecisionExecutor.MaximumStageDuration);
        if (stage.ExpiresAtUtc is { } expiry && expiry < deadline) deadline = expiry;
        return view.ExpiresAtUtc < deadline ? view.ExpiresAtUtc : deadline;
    }

    static StrategyWorkflowStageState CurrentStage(IntrinsicTimeStrategyWorkflowView view) => view.CurrentStage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => view.RegimeDiscovery,
        StrategyWorkflowStage.MarketCondition => view.MarketCondition,
        StrategyWorkflowStage.TradeSelection => view.TradeSelection,
        StrategyWorkflowStage.OrderComposition => view.OrderComposition,
        StrategyWorkflowStage.RiskManagement => view.RiskManagement,
        _ => throw new ArgumentException("A decision stage is required.", nameof(view))
    };

    sealed class RegimeOperator : IWorkflowDecisionOperator<RegimeDiscoveryCalculationInput, RegimeDiscoveryResult>
    {
        readonly RegimeDiscoveryCalculationModel model = new();
        public async ValueTask<RegimeDiscoveryResult> CalculateAsync(RegimeDiscoveryCalculationInput input, CancellationToken token) =>
            await model.CalculateAsync(input, RegimeDiscoveryExecutionMode.Sequential, token).ConfigureAwait(false);
    }
    sealed record AssessmentInput(ExecuteMarketConditionAssessmentCommand Command, MarketConditionAssessmentSnapshot Snapshot);
    sealed class AssessmentOperator : IWorkflowDecisionOperator<AssessmentInput, MarketConditionAssessmentResult>
    {
        readonly MarketConditionAssessmentCalculator model = new();
        public ValueTask<MarketConditionAssessmentResult> CalculateAsync(AssessmentInput input, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(model.CalculatePrepared(input.Command, input.Snapshot, input.Command.CommandId)); }
    }
    sealed record SelectionInput(ExecuteTradeSelectionPipelineCommand Command, PreparedTradeSelectionConfiguration Configuration);
    sealed class SelectionOperator : IWorkflowDecisionOperator<SelectionInput, TradeSelectionResult>
    {
        public ValueTask<TradeSelectionResult> CalculateAsync(SelectionInput input, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(TradeSelectionEvaluator.EvaluatePrepared(input.Command, input.Configuration)); }
    }
    sealed record CompositionInput(ExecuteOrderCompositionPipelineCommand Command, PreparedCompositionConfiguration Configuration);
    sealed class CompositionOperator : IWorkflowDecisionOperator<CompositionInput, OrderCompositionResult>
    {
        readonly OrderComposer.Model.OrderComposer model = new(new Black76ComposerPricer());
        public ValueTask<OrderCompositionResult> CalculateAsync(CompositionInput input, CancellationToken token) =>
            ValueTask.FromResult(model.CalculatePrepared(input.Command, input.Configuration, token));
    }
    sealed class RiskOperator : IWorkflowDecisionOperator<ExecuteRiskManagementPipelineCommand, RiskAssessmentResult>
    {
        readonly RiskEvaluator model = new();
        public ValueTask<RiskAssessmentResult> CalculateAsync(ExecuteRiskManagementPipelineCommand input, CancellationToken token) =>
            ValueTask.FromResult(model.Calculate(input, token));
    }
    sealed record PortfolioInput(EvaluatePortfolioOrderCompositionCommand Request, PortfolioDecisionFinancialState Financial);
    sealed class PortfolioOperator(IPortfolioEntryDecisionCalculator calculator, TimeProvider clock, WorkflowDecisionSession session)
        : IWorkflowDecisionOperator<PortfolioInput, PortfolioEntryApproval>
    {
        public ValueTask<PortfolioEntryApproval> CalculateAsync(PortfolioInput input, CancellationToken token)
        {
            var funds = calculator.Calculate(input.Request, input.Financial, token);
            var now = clock.GetUtcNow().UtcDateTime;
            var until = new[] { input.Request.ExpiresAtUtc, input.Financial.ValidUntilUtc }.Min();
            return ValueTask.FromResult(new PortfolioEntryApproval { Request = input.Request, Funds = funds,
                ProducedAtUtc = now, ValidUntilUtc = until, DecisionSessionId = session.Id }.Seal());
        }
    }
}

public sealed class WorkflowDecisionException(DecisionExecutionStatus status, Exception? inner = null)
    : Exception($"WF.DECISION.{status.ToString().ToUpperInvariant()}", inner)
{
    public DecisionExecutionStatus Status { get; } = status;
}
