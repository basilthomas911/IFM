using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed record SubscriptionLeaseView(
    SubscriptionLeaseToken Token, SubscriptionOwnerKey Owner, SubscriptionTarget Target,
    SubscriptionLeasePurpose Purpose, DateTimeOffset? ExpiresAtUtc)
{
    public bool IsDurable => Purpose is SubscriptionLeasePurpose.Strategy
        or SubscriptionLeasePurpose.WorkingOrder or SubscriptionLeasePurpose.Position;
}
