namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

public enum BusinessSubscriptionSourceKind { IntrinsicTimeWorkflow, TradeOrder, TradePosition }

/// <summary>Identifies a committed source event; no client-supplied leases or authority status.</summary>
public sealed record BusinessSubscriptionSourceReference(BusinessSubscriptionSourceKind Kind, string EntityId,
    long Version, Guid EventId, long EventLogId = 0);

/// <summary>
/// Implemented by domain adapters that reload the committed aggregate/event. Missing or uncertain authority
/// yields no new handoff; explicit Unknown facts preserve ownership through the durable store.
/// </summary>
public interface ICommittedBusinessSubscriptionSource
{
    Task<DurableAuthorityMutation?> ReadAsync(BusinessSubscriptionSourceReference source, CancellationToken cancellationToken);
}
