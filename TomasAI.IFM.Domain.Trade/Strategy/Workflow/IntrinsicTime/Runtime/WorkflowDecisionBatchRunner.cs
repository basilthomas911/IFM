using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>
/// Runs after durable admission, under the existing per-assignment Command owner. Intermediate evidence stays private;
/// the caller persists the entire result batch before dispatching any financial commitment.
/// </summary>
public sealed class WorkflowDecisionBatchRunner(Func<IIntrinsicTimeStrategyWorkflowRealtimeContext> contextFactory,
    ILogger<WorkflowDecisionBatchRunner>? logger = null)
{
    // The externally visible stage budget includes applying its terminal state.
    // Stop waiting for calculation early enough to settle that state before the
    // unchanged two-second acceptance deadline; late calculator replies remain fenced.
    internal static readonly TimeSpan TerminalSettlementReserve = TimeSpan.FromMilliseconds(100);
    internal sealed record Evidence(RedispatchCurrentStrategyPipelineCommand Command, IReadOnlyList<ICommand> Commands);
    internal async Task<Evidence> RunAsync(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> owner, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var batchStarted=Stopwatch.GetTimestamp();
        var initial = state.DecisionView ?? throw new InvalidOperationException("WF.DECISION.STATE_NOT_READY");
        var services = contextFactory();
        var context = new LocalContext(services, state);
        var sourceCommands = new List<ICommand>();
        try
        {
            // Five operators plus the separate composition capture/acceptance transition. No transition may loop.
            for (var step = 0; step < 8; step++)
            {
                var current = state.DecisionView!;
                if (current.Status != WorkflowStrategyMachineStatus.Started || current.EntryCommit is not null) break;
                var stageDeadline=WorkflowCalculationRuntime.EffectiveDeadline(current);
                var stageRemaining=stageDeadline-services.TimeProvider.GetUtcNow().UtcDateTime;
                var batchRemaining=WorkflowDecisionExecutor.MaximumStageDuration-Stopwatch.GetElapsedTime(batchStarted);
                var remaining=stageRemaining<batchRemaining?stageRemaining:batchRemaining;
                if (remaining <= TerminalSettlementReserve)
                {
                    await ApplyOwnedAsync(Stop(current, services.TimeProvider.GetUtcNow().UtcDateTime>=stageDeadline
                        ? WorkflowLocalDispatcher.DeadlineSource : WorkflowLocalDispatcher.InterruptedSource)).ConfigureAwait(false);
                    WorkflowDecisionTelemetry.RecordBoundary(current, state.DecisionView!, services.TimeProvider.GetUtcNow().UtcDateTime);
                    break;
                }
                var source = state.LatestSource ?? throw new InvalidOperationException("WF.DECISION.SOURCE_NOT_READY");
                await IntrinsicTimeStrategyWorkflowRealtimeActor.DispatchCommittedStateAsync(context, source).AsTask()
                    .WaitAsync(remaining-TerminalSettlementReserve, services.TimeProvider).ConfigureAwait(false);
                while (context.Commands.TryDequeue(out var command))
                    await ApplyOwnedAsync(command).ConfigureAwait(false);
                if (state.DecisionView!.CurrentStage != current.CurrentStage || state.DecisionView.Status != WorkflowStrategyMachineStatus.Started
                    || state.DecisionView.EntryCommit is not null)
                    WorkflowDecisionTelemetry.RecordBoundary(current, state.DecisionView, services.TimeProvider.GetUtcNow().UtcDateTime);
                if (state.DecisionView!.WorkflowRevision == current.WorkflowRevision)
                    throw new InvalidOperationException("WF.DECISION.NO_CONTINUATION");
            }
            if (state.DecisionView is { Status: WorkflowStrategyMachineStatus.Started, EntryCommit: null } unfinished)
                await ApplyOwnedAsync(Stop(unfinished, WorkflowLocalDispatcher.InterruptedSource)).ConfigureAwait(false);
        }
        catch (Exception error) when (state.DecisionView is { Status: WorkflowStrategyMachineStatus.Started, EntryCommit: null })
        {
            // A failed attempt is terminal; neither the operator nor its input capture is retried.
            logger?.LogWarning(error, "Decision batch stopped for workflow {WorkflowId} at {Stage}.", initial.WorkflowId, state.DecisionView!.CurrentStage);
            context.Close();
            // A reserved-budget timeout precedes the authoritative expiry. Use
            // interruption in that case; the owner's expiry guard must not be
            // weakened merely to settle early.
            await ApplyOwnedAsync(Stop(state.DecisionView!, error is TimeoutException
                && services.TimeProvider.GetUtcNow().UtcDateTime >= WorkflowCalculationRuntime.EffectiveDeadline(state.DecisionView!)
                ? WorkflowLocalDispatcher.DeadlineSource : WorkflowLocalDispatcher.InterruptedSource)).ConfigureAwait(false);
        }
        finally { context.Close(); }
        var result = Stop(initial, "WorkflowDecisionEvidence");
        return new(result, sourceCommands.ToArray());

        async Task ApplyOwnedAsync(ICommand command)
        {
            await ApplyAsync(command, owner, state).ConfigureAwait(false);
            sourceCommands.Add(command);
        }

        RedispatchCurrentStrategyPipelineCommand Stop(IntrinsicTimeStrategyWorkflowView view, string reason) => new()
        {
            CommandId = Guid.NewGuid(), EntityId = view.EntityId, WorkflowId = view.WorkflowId,
            ExpectedWorkflowRevision = view.WorkflowRevision, ExpectedStage = view.CurrentStage,
            Subject = new(ActorType.Command, RedispatchCurrentStrategyPipelineCommand.Actor,
                RedispatchCurrentStrategyPipelineCommand.Verb, view.EntityId.Format()),
            RequestedAtUtc = services.TimeProvider.GetUtcNow().UtcDateTime, RequestedBy = reason
        };
    }

    static async Task ApplyAsync(ICommand command, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> owner,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var result = await IntrinsicTimeStrategyWorkflowCommandActor.ApplyLocalDecisionAsync(command, owner, state).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException("WF.DECISION.CONTINUATION_REJECTED");
    }

    /// <summary>No transport/query capabilities are available to decision adapters, including accidental legacy fallbacks.</summary>
    sealed class LocalContext(IIntrinsicTimeStrategyWorkflowRealtimeContext inner, IntrinsicTimeStrategyWorkflowCommandState state)
        : IIntrinsicTimeStrategyWorkflowRealtimeContext
    {
        internal System.Collections.Concurrent.ConcurrentQueue<ICommand> Commands { get; } = new();
        int closed;
        internal void Close() => Interlocked.Exchange(ref closed, 1);
        public ValueTask SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId
        {
            if (Volatile.Read(ref closed) != 0) return ValueTask.CompletedTask;
            if (entityId.Format() != state.EntityId.Format()) throw Forbidden();
            Commands.Enqueue(command);
            return ValueTask.CompletedTask;
        }
        public Task<IntrinsicTimeStrategyWorkflowView?> ReadActiveWorkflowAsync(IntrinsicTimeStrategyWorkflowEntityId entityId, CancellationToken token = default)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(entityId == state.EntityId ? state.DecisionView : null); }
        public TimeProvider TimeProvider => inner.TimeProvider;
        public Microsoft.Extensions.Logging.ILogger<IntrinsicTimeStrategyWorkflowRealtimeActor> Logger => inner.Logger;
        public IntrinsicTimeStrategyWorkflowOptions Options => inner.Options;
        public RegimeDiscovery.Options.RegimeDiscoveryExecutionOptions RegimeDiscoveryExecutionOptions => inner.RegimeDiscoveryExecutionOptions;
        public Domain.Reference.Shared.ParameterSets.IParameterRuntimeSnapshot? ParameterRuntime => inner.ParameterRuntime;
        public WorkflowPreparedConfigurationCache? PreparedConfiguration => inner.PreparedConfiguration;
        public WorkflowCalculationRuntime? DecisionRuntime => inner.DecisionRuntime;
        public WorkflowCompositionInputCache? CompositionInputs => inner.CompositionInputs;
        public Domain.Portfolio.Shared.OrderComposition.IPortfolioDecisionStateReader? FinancialDecisionState => inner.FinancialDecisionState;
        public MarketCondition.Model.IMarketConditionAssessmentSnapshotProvider? PreparedMarketContext => inner.PreparedMarketContext;
        public Domain.MarketData.Analytics.Shared.RegimeDiscovery.IRegimeDiscoveryMarketSignalSnapshotProvider RegimeDiscoverySnapshotProvider => inner.RegimeDiscoverySnapshotProvider;
        public Domain.MarketData.Shared.IValueDateProvider ValueDateProvider => inner.ValueDateProvider;
        public Application.MarketData.Contracts.IMarketDataApi MarketDataApi => inner.MarketDataApi;
        public Application.Storage.ConfigurationDb.IConfigurationDbContext ConfigurationDb => throw Forbidden();
        public Domain.Portfolio.Shared.ServiceApi.IPortfolioQueryApi PortfolioQueries => throw Forbidden();
        public Domain.Portfolio.Shared.ServiceApi.IPortfolioFundCommandApi PortfolioCommands => throw Forbidden();
        public Domain.Portfolio.Shared.OrderComposition.IPortfolioOrderCompositionApi PortfolioOrderComposition => throw Forbidden();
        public ActorMailboxId ActorId => inner.ActorId;
        public IContainerInstance Container => throw Forbidden();
        public bool IsReady => inner.IsReady;
        public bool SetMessageInfo(ActorThreadId id, ActorMessageInfo info) => throw Forbidden();
        public ActorMessageInfo? GetMessageInfo(ActorThreadId id) => null;
        public ValueTask SendAsync<TEvent, TEntityId>(TEvent value) where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId => throw Forbidden();
        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query) where TQuery : class, IQuery<TResult> where TResult : class => throw Forbidden();
        public ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command) where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId => throw Forbidden();
        public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(TCommand command, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class => throw Forbidden();
        public void AddEventRouter(ActorTypeId from, ActorMailboxId to) => throw Forbidden();
        public void RemoveEventRouter(ActorTypeId from, ActorMailboxId to) => throw Forbidden();
        public void AddRealtimeRouter(ActorTypeId from, ActorMailboxId to) => throw Forbidden();
        public void AddRealtimeRouter(ActorTypeId from, ActorMailboxId to, Func<ActorSubject, string> projection) => throw Forbidden();
        public void RemoveRealtimeRouter(ActorTypeId from, ActorMailboxId to) => throw Forbidden();
        static InvalidOperationException Forbidden() => new("WF.DECISION.IO_FORBIDDEN");
    }
}
