using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>Commits an accepted plan event and its required plan/activity rows in one PostgreSQL transaction.</summary>
internal static class TradePlanAtomicPersistence
{
    internal static async Task CommitAsync(ICommand request, DomainEventCollection pending, long expectedVersion,
        IEventSourceActorDbContext source, IDbContextFactory factory, IPostgresEventTransaction transactions,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(source.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            || !string.Equals(factory.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            || PostgresDatabaseIdentity.Parse(source.ConnectionString) != PostgresDatabaseIdentity.Parse(factory.TradePlanDb.ConnectionString))
            throw new InvalidOperationException("Trade plan source and projection must share one PostgreSQL database/security identity.");
        await transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ReserveCommandAsync(request, token).ConfigureAwait(false);
            var version = expectedVersion;
            foreach (var sourceEvent in pending)
            {
                var plan = sourceEvent switch
                {
                    FuturesTradePlanUpdatedEvent futures => futures.Plan,
                    VerticalSpreadTradePlanUpdatedEvent vertical => vertical.Plan,
                    _ => throw new InvalidOperationException("Unsupported atomic trade plan event: " + sourceEvent.GetType().Name)
                };
                await transaction.AppendAsync(request.StreamId, request.CommandId, sourceEvent, version++, token).ConfigureAwait(false);
                await factory.TradePlanDb.ProjectMaterialAsync(transaction, plan, token).ConfigureAwait(false);
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
