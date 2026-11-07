using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;

/// <summary>Restores the broker-order projection from committed full-state events before actor intake.</summary>
public sealed class BrokerOrderReadModelRebuilder(IPostgresEventTransaction transactions)
{
    private const string LatestSnapshots = """
        WITH latest AS (
            SELECT DISTINCT ON (log.eventstreamid) log.eventversion, name.eventtypename
            FROM public.event_name_id name
            JOIN public.event_log log USING (eventnameid)
            WHERE name.eventname = $1
            ORDER BY log.eventstreamid, log.streamversion DESC
        )
        SELECT latest.eventtypename, log.eventversion, log.eventpayload
        FROM latest JOIN public.event_log log USING (eventversion);
        """;

    /// <summary>Restores the latest committed definition for each broker component without dispatching anything.</summary>
    public async Task<int> RebuildAsync(IBrokerOrderReadStore store, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var snapshots = await transactions.ExecuteOnceAsync((transaction, token) =>
            transaction.QueryAsync(LatestSnapshots, [nameof(BrokerOrderChangedEvent)],
                reader => (BrokerOrderChangedEvent)new EventStreamReadModel
                {
                    EventTypeName = reader.GetString(0),
                    EventVersion = reader.GetInt64(1),
                    EventData = reader.GetFieldValue<byte[]>(2)
                }.ToDomainEvent(), token), cancellationToken).ConfigureAwait(false);
        foreach (var snapshot in snapshots)
            store.Set(snapshot.BrokerOrderDefinition);
        return snapshots.Count;
    }
}
