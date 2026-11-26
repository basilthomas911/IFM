using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Committed owner references move between stages locally, without a Function or Realtime transport hop.</summary>
public sealed partial class WorkflowLocalDispatcher(Func<IIntrinsicTimeStrategyWorkflowRealtimeContext> contextFactory,
    WorkflowResidentStateRegistry resident, WorkflowCalculationRuntime runtime, ILogger<WorkflowLocalDispatcher> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    public const string DeadlineSource = "WorkflowDecisionDeadline";
    public const string InterruptedSource = "WorkflowDecisionInterrupted";
    public const string OverloadedSource = "WorkflowDecisionOverloaded";
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly Lazy<IIntrinsicTimeStrategyWorkflowRealtimeContext> localContext = new(contextFactory);
    readonly Channel<WorkflowStrategyStateUpdatedEvent> work = Channel.CreateBounded<WorkflowStrategyStateUpdatedEvent>(128);
    readonly Channel<WorkflowStrategyStateUpdatedEvent> entries = Channel.CreateBounded<WorkflowStrategyStateUpdatedEvent>(64);
    readonly ConcurrentDictionary<(Guid Workflow, long Revision), byte> deadlineSent = new();
    readonly ConcurrentDictionary<(Guid Workflow, long Revision), byte> overloaded = new();
    readonly ConcurrentDictionary<(Guid Workflow, long Revision), DateTime> scheduled = new();
    internal bool ReadyForRecovery => localContext.Value.IsReady;

    public void Dispatch(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        if (snapshot.State.Status != WorkflowStrategyMachineStatus.Started) return;
        if (scheduled.Count >= 256)
            foreach (var old in scheduled.Where(value => value.Value <= clock.GetUtcNow().UtcDateTime).Select(value => value.Key)) scheduled.TryRemove(old, out _);
        var identity = (snapshot.WorkflowId.Value, snapshot.WorkflowRevision);
        if (scheduled.Count >= 1024)
        {
            overloaded.TryAdd(identity, 0);
            return;
        }
        if (!scheduled.TryAdd(identity, snapshot.State.ExpiresAtUtc)) return;
        var queue = snapshot.State.EntryCommit is null ? work : entries;
        if (!queue.Writer.TryWrite(snapshot)) overloaded.TryAdd((snapshot.WorkflowId.Value, snapshot.WorkflowRevision), 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) =>
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RunWorkerAsync(work, stoppingToken))
            .Concat(Enumerable.Range(0, 2).Select(_ => RunWorkerAsync(entries, stoppingToken)))
            .Append(WatchDeadlinesAsync(stoppingToken))).ConfigureAwait(false);

    async Task RunWorkerAsync(Channel<WorkflowStrategyStateUpdatedEvent> queue, CancellationToken token)
    {
        await foreach (var snapshot in queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
        {
            try
            {
                var context = localContext.Value;
                var view = resident.ReadCurrent(snapshot.EntityId.Format());
                if (view is null || view.WorkflowId != snapshot.WorkflowId || view.WorkflowRevision != snapshot.WorkflowRevision) continue;
                if (view.EntryCommit is null && view.DecisionRuntimeSessionId != runtime.SessionId)
                    await StopAsync(context, view, InterruptedSource).ConfigureAwait(false);
                else if (ExecuteIntrinsicTimeStrategyWorkflow.CanExpire(view, clock.GetUtcNow().UtcDateTime))
                    await StopAsync(context, view, DeadlineSource).ConfigureAwait(false);
                else await IntrinsicTimeStrategyWorkflowRealtimeActor.DispatchCommittedStateAsync(context, snapshot).ConfigureAwait(false);
            }
            catch (Exception error) when (!token.IsCancellationRequested) { DispatchFailed(logger, snapshot.WorkflowId.ToString(), error); }
        }
    }

    async Task WatchDeadlinesAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50), clock);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            var active = resident.ActiveViews();
            var keys = active.Select(value => (value.WorkflowId.Value, value.WorkflowRevision)).ToHashSet();
            foreach (var old in deadlineSent.Keys.Where(key => !keys.Contains(key))) deadlineSent.TryRemove(old, out _);
            foreach (var old in overloaded.Keys.Where(key => !keys.Contains(key))) overloaded.TryRemove(old, out _);
            foreach (var view in active.Where(value => value.DecisionRuntimeVersion > 0))
            {
                if (overloaded.TryRemove((view.WorkflowId.Value, view.WorkflowRevision), out _))
                {
                    try { await StopAsync(localContext.Value, view, OverloadedSource).ConfigureAwait(false); }
                    catch (Exception error) when (!token.IsCancellationRequested) { DispatchFailed(logger, view.WorkflowId.ToString(), error); }
                    continue;
                }
                if (!ExecuteIntrinsicTimeStrategyWorkflow.CanExpire(view, clock.GetUtcNow().UtcDateTime)
                    || !deadlineSent.TryAdd((view.WorkflowId.Value, view.WorkflowRevision), 0)) continue;
                try { await StopAsync(localContext.Value, view, DeadlineSource).ConfigureAwait(false); }
                catch (Exception error) when (!token.IsCancellationRequested) { DispatchFailed(logger, view.WorkflowId.ToString(), error); }
            }
        }
    }

    internal async Task RecoverAsync(IntrinsicTimeStrategyWorkflowView view)
    {
        if (view.EntryCommit is not null)
            await RiskManager.Realtime.PreparedPortfolioRisk.CommitAsync(view, localContext.Value, reconcileOnly: true).ConfigureAwait(false);
        else if (view.DecisionRuntimeSessionId != runtime.SessionId)
            await StopAsync(localContext.Value, view, InterruptedSource).ConfigureAwait(false);
        else if (ExecuteIntrinsicTimeStrategyWorkflow.CanExpire(view, clock.GetUtcNow().UtcDateTime))
            await StopAsync(localContext.Value, view, DeadlineSource).ConfigureAwait(false);
    }
    internal Task InterruptAsync(IntrinsicTimeStrategyWorkflowView view) => StopAsync(localContext.Value, view, InterruptedSource);

    static async Task StopAsync(IIntrinsicTimeStrategyWorkflowRealtimeContext context, IntrinsicTimeStrategyWorkflowView view, string source)
    {
        var command = new RedispatchCurrentStrategyPipelineCommand
        {
            CommandId = Realtime.WorkflowPublicationDelivery.Key(view,"local-stop/"+source), EntityId = view.EntityId, WorkflowId = view.WorkflowId,
            ExpectedWorkflowRevision = view.WorkflowRevision, ExpectedStage = view.CurrentStage,
            Subject = new(ActorType.Command, RedispatchCurrentStrategyPipelineCommand.Actor, RedispatchCurrentStrategyPipelineCommand.Verb, view.EntityId.Format()),
            RequestedAtUtc = view.UpdatedAtUtc, RequestedBy = source
        };
        await Realtime.WorkflowPublicationDelivery.PublishAsync(context,view,command,"local-stop/"+source).ConfigureAwait(false);
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Local workflow dispatch stopped for {WorkflowId}.")]
    static partial void DispatchFailed(ILogger logger, string workflowId, Exception error);
}
