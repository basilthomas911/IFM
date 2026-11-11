using TomasAI.IFM.Application.EventSourcing.Persistence.Command;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>Commits complete exit-stage source facts and their read rows before the next actor handoff.</summary>
internal static class ExitWorkflowAtomicPersistence
{
    internal static async Task CommitAsync(ICommand command, DomainEventCollection pending, long expectedVersion,
        IEventSourceActorDbContext source, IDbContextFactory factory, IPostgresEventTransaction? transactions,
        CancellationToken cancellationToken = default, bool projectWorkflow = true)
    {
        if (pending.Count == 0) return;
        if (!string.Equals(source.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            || projectWorkflow && (!string.Equals(factory.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
                || PostgresDatabaseIdentity.Parse(source.ConnectionString) != PostgresDatabaseIdentity.Parse(factory.TradePlanDb.ConnectionString)))
            throw new InvalidOperationException("Exit workflow source and projection must share one PostgreSQL database/security identity.");
        var owner = transactions ?? throw new InvalidOperationException("Atomic exit workflow persistence requires the PostgreSQL transaction service.");
        await owner.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ReserveCommandAsync(command, token).ConfigureAwait(false);
            var version = expectedVersion;
            foreach (var sourceEvent in pending)
            {
                await ExitWorkflowReceiptStore.VerifyAsync(transaction, command, sourceEvent, token).ConfigureAwait(false);
                await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, command.CommandId, sourceEvent, version++), token).ConfigureAwait(false);
                if (projectWorkflow)
                    await factory.TradePlanDb.ProjectExitWorkflowAsync(transaction, TradePlanProjectionWriter.PrepareExitStage(ExitWorkflowSnapshotFacts.From(sourceEvent)), token).ConfigureAwait(false);
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
