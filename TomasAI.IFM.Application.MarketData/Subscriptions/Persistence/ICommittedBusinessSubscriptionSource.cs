using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>
/// Implemented by domain adapters that reload the committed aggregate/event. Missing or uncertain authority
/// yields no new handoff; explicit Unknown facts preserve ownership through the durable store.
/// </summary>
public interface ICommittedBusinessSubscriptionSource
{
    Task<DurableAuthorityMutation?> ReadAsync(BusinessSubscriptionSourceReference source, CancellationToken cancellationToken);
    Task<DurableAuthorityMutation?> ReadCommittedAsync(IEvent source, BusinessSubscriptionSourceKind kind,
        long sourceVersion, long eventStreamId, long eventLogId, CancellationToken cancellationToken);
}
