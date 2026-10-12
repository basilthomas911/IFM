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
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(source.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            || !string.Equals(factory.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            || PostgresDatabaseIdentity.Parse(source.ConnectionString) != PostgresDatabaseIdentity.Parse(factory.TradePlanDb.ConnectionString))
            throw new InvalidOperationException("Exit workflow source and projection must share one PostgreSQL database/security identity.");
        var owner = transactions ?? throw new InvalidOperationException("Atomic exit workflow persistence requires the PostgreSQL transaction service.");
        await owner.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ReserveCommandAsync(command, token).ConfigureAwait(false);
            var version = expectedVersion;
            foreach (var sourceEvent in pending)
            {
                var snapshot = ExitWorkflowSnapshotFacts.From(sourceEvent);
                await transaction.AppendAsync(command.StreamId, command.CommandId, sourceEvent, version++, token).ConfigureAwait(false);
                await factory.TradePlanDb.ProjectExitWorkflowAsync(transaction, snapshot, token).ConfigureAwait(false);
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
