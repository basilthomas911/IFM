using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Registers durable live handoff and side-effect-free read-model rebuild for each workflow source type.</summary>
internal static class ExitWorkflowProjectionDescriptors
{
    internal static IReadOnlyCollection<EventProjectionDescriptor> Create(ITradePlanDbContext store, Func<IActorProducer> producer,
        IPostgresEventTransaction transactions)
        => ExitWorkflowSnapshotFacts.CheckpointTypes.Select(type => new EventProjectionDescriptor(
            type, EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (source, execution) =>
            {
                if (!string.Equals(store.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                    await store.ProjectExitWorkflowAsync(TradePlanProjectionWriter.PrepareExitStage(ExitWorkflowSnapshotFacts.From(source)), execution.CancellationToken).ConfigureAwait(false);
                if (ExitWorkflowLegacyReconciliation.IsHistorical(source))
                    _ = await ExitWorkflowLegacyReconciliation.AdvanceAsync(source, transactions, producer(), execution.CancellationToken).ConfigureAwait(false);
                else
                    await ExitWorkflowContinuation.ApplyAsync(source, producer(), execution.CancellationToken).ConfigureAwait(false);
                return new(EventProjectionApplyOutcome.Applied);
            },
            static _ => null, static (_, _) => null,
            publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: false)
            .WithReadModelRebuild((source, token) => store.ProjectExitWorkflowAsync(TradePlanProjectionWriter.PrepareExitStage(ExitWorkflowSnapshotFacts.From(source)), token)))
            .ToArray();
}
