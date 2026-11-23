using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Process-local authority published only after acknowledged commits or verified startup hydration.</summary>
public sealed class WorkflowResidentStateRegistry
{
    readonly ConcurrentDictionary<string, IntrinsicTimeStrategyWorkflowCommandState> states = new(StringComparer.Ordinal);
    internal sealed record UncertainWrite(IntrinsicTimeStrategyWorkflowView View, string Stream);
    readonly ConcurrentDictionary<Guid, UncertainWrite> uncertain = new();
    internal void MarkUncertain(IntrinsicTimeStrategyWorkflowView view, string stream) => uncertain[view.WorkflowId.Value] = new(view, stream);
    internal void ConfirmOutcome(Guid workflow) => uncertain.TryRemove(workflow, out _);
    internal UncertainWrite[] UncertainOutcomes() => uncertain.Values.Take(64).ToArray();

    internal void PublishCommitted(string entity, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var committed = state.ForkCommitted();
        states.AddOrUpdate(entity, committed, (_, previous) =>
            previous.PersistedStreamVersion > committed.PersistedStreamVersion ? previous : committed);
        // Keep terminal owners hot: the next signal needs the retained DC state, queue metadata and stream revision.
        // Evict old terminal owners only; active/uncertain financial work is never discarded to meet a cache target.
        if (states.Count > 256)
            foreach (var old in states.Where(value => !value.Value.HasActiveWorkflow && value.Value.NextSignal is null
                    && value.Key != entity).OrderBy(value => value.Value.TrustedCommittedView?.UpdatedAtUtc)
                .Take(Math.Min(64, states.Count - 256)))
                states.TryRemove(old);
    }

    internal bool TryFork(string entity, out IntrinsicTimeStrategyWorkflowCommandState? state)
    {
        state = states.TryGetValue(entity, out var committed) ? committed.ForkCommitted() : null;
        return state is not null;
    }

    public IntrinsicTimeStrategyWorkflowView? ReadCurrent(string entity) =>
        states.TryGetValue(entity, out var committed) ? committed.TrustedCommittedView : null;

    public void Invalidate(string entity) => states.TryRemove(entity, out _);
    internal IntrinsicTimeStrategyWorkflowView[] ActiveViews() => states.Values.Select(value => value.TrustedCommittedView)
        .Where(value => value is { Status: WorkflowStrategyMachineStatus.Started }).Select(value => value!).ToArray();
}
