using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

public sealed record DurableAuthorityState(
    string SourceId, long SourceVersion, Guid SourceEventId, string FactDigest,
    DurableSubscriptionOwner Owner, DurableAuthorityStatus Status, string ReasonCode,
    IReadOnlyList<DurableSubscriptionLease> Leases);
