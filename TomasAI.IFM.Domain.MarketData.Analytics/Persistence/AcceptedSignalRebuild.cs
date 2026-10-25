using TomasAI.IFM.Application.EventSourcing.Projections.MarketData;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Analytics.Persistence;
/// <summary>Explicit materialization-only reconstruction from saved analytic facts.</summary>
internal static class AcceptedSignalRebuild
{
    internal static async Task MaterializeAsync(IDbContextFactory databases, IPostgresEventTransaction? transactions,
        IEvent source, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(databases);
        if (!string.Equals(databases.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            throw new InvalidOperationException("Accepted signal reconstruction requires a PostgreSQL MarketDataDb target.");
        var owner = transactions ?? throw new InvalidOperationException("Signal reconstruction requires the PostgreSQL event transaction service.");
        await owner.ExecuteOnceAsync(async (transaction,cancellation) =>
        {
            if (!await AcceptedMarketSignalProjection.TryProjectAsync(transaction,source,cancellation).ConfigureAwait(false))
                throw new InvalidOperationException("No audited signal materialization exists for this source event.");
            return true;
        },token).ConfigureAwait(false);
    }
}
