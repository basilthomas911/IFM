using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Validated server-authorized scope plus the existing workflow identity; not an authorization token.</summary>
public sealed record SubscriptionOwnerKey
{
    public string Scope { get; }
    public TickerStreamOwner Owner { get; }

    public SubscriptionOwnerKey(string scope, TickerStreamOwner owner)
    {
        SubscriptionIdentity.Validate(scope, 128);
        owner.Validate();
        SubscriptionIdentity.Validate(owner.WorkflowType, 64);
        SubscriptionIdentity.Validate(owner.WorkflowId, 128);
        SubscriptionIdentity.Validate(owner.LegId, 128);
        Scope = scope;
        Owner = owner;
    }
}
