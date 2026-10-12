using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed record SubscriptionAcquireRequest(
    Guid OperationId, Guid HostEpochId, Guid CorrelationId, SubscriptionOwnerKey Owner, SubscriptionTarget Target,
    SubscriptionLeasePurpose Purpose, DateTimeOffset DeadlineUtc);
