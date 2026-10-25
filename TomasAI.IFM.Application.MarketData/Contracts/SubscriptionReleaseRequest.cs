using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;
public sealed record SubscriptionReleaseRequest(
    Guid OperationId, Guid CorrelationId, SubscriptionOwnerKey Owner, SubscriptionLeaseToken Lease,
    DateTimeOffset DeadlineUtc);
