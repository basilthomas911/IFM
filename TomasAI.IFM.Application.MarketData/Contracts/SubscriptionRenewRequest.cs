using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;
public sealed record SubscriptionRenewRequest(
    Guid OperationId, Guid CorrelationId, SubscriptionOwnerKey Owner, SubscriptionLeaseToken Lease,
    DateTimeOffset DeadlineUtc);
