using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Identifies a committed source event; no client-supplied leases or authority status.</summary>
public sealed record BusinessSubscriptionSourceReference(BusinessSubscriptionSourceKind Kind, string EntityId,
    long Version, Guid EventId, long EventLogId = 0);
