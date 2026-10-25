using TomasAI.IFM.Application.EventSourcing.Projections.TradePlan;
using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>Reconstructs exit workflow stages from accepted source facts without executing an exit or calling Portfolio.</summary>
public static class AcceptedExitWorkflowReadProjectionDescriptors
{
    /// <summary>Creates materialization-only actions for controlled reconstruction of the workflow table.</summary>
    public static IReadOnlyCollection<EventProjectionDescriptor> Create(IDbContextFactory databases, IPostgresEventTransaction transactions)
    {
        ArgumentNullException.ThrowIfNull(databases);
        ArgumentNullException.ThrowIfNull(transactions);
        return ImmutableArray.Create(
            Describe<ExitPositionWorkflowStartedEvent>(),
            Describe<ExitOrderCompositionCompletedEvent>(),
            Describe<PositionExitRiskCompletedEvent>(),
            new EventProjectionDescriptor(typeof(ExitWorkflowProjectionBaselineEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
                (_, _) => throw new InvalidOperationException("Use only the verified baseline materialization action."),
                _ => null, (_, _) => null, publishProcessingEvent: false, useDurableReplay: false, publishTerminalEvent: false)
                .WithReadModelRebuild((source, token) => new ExitWorkflowProjectionBaseline(transactions, databases.TradePlanDb)
                    .MaterializeAsync((ExitWorkflowProjectionBaselineEvent)source, token)));

        EventProjectionDescriptor Describe<TEvent>() where TEvent : IEvent =>
            new EventProjectionDescriptor(typeof(TEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
                (_, _) => throw new InvalidOperationException("Exit workflow reconstruction must use only its recorded snapshot facts."),
                _ => null, (_, _) => null, publishProcessingEvent: false, useDurableReplay: false, publishTerminalEvent: false)
                .WithReadModelRebuild((source, token) => transactions.ExecuteOnceAsync(async (transaction, cancellation) =>
                {
                    await databases.TradePlanDb.ProjectExitWorkflowAsync(transaction, ExitWorkflowSnapshotFacts.From(source), cancellation).ConfigureAwait(false);
                    return true;
                }, token));
    }
}
