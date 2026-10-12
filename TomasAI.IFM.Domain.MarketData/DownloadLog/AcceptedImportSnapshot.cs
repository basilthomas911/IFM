using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog;

/// <summary>Loads one accepted import outcome so delivery retries never reacquire provider data.</summary>
internal static class AcceptedImportSnapshot
{
    internal static async Task<TEvent?> LoadAsync<TEvent>(IDbContextFactory databases,string stream)
        where TEvent : class,IEvent
    {
        if (!string.Equals(databases.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal))
            return null;
        var id = await databases.ActorEventSourceDb.GetEventStreamIdFromDbAsync(stream).ConfigureAwait(false);
        if (id is null) return null;
        var rows = await databases.ActorEventSourceDb.GetEventsFromSnapshotAsync<TEvent>(id.EventStreamId).ConfigureAwait(false);
        return rows.Select(row => row.ToDomainEvent()).OfType<TEvent>().LastOrDefault();
    }
}
