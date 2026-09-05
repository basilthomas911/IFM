namespace TomasAI.IFM.Application.MarketData.Subscriptions;

/// <summary>Explicit disabled rollout guard while only the offline foundation is implemented.</summary>
public sealed record Stage4SubscriptionOptions
{
    public bool Enabled { get; init; }
    public TickerLeasePolicy Leases { get; init; } = new();

    public Stage4SubscriptionOptions ValidateForApplicationStartup()
    {
        ArgumentNullException.ThrowIfNull(Leases);
        Leases.Validate();
        if (Enabled)
            throw new InvalidOperationException("Stage 4 is offline implementation only: durable authority, worker option routing, pricing and acceptance are not qualified. Application enablement is prohibited.");
        return this;
    }
}
