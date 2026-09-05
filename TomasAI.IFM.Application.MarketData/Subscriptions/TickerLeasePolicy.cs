namespace TomasAI.IFM.Application.MarketData.Subscriptions;

/// <summary>Offline engineering defaults only; this policy does not enable Stage 4 or approve live capacity.</summary>
public sealed record TickerLeasePolicy
{
    public TimeSpan EphemeralTimeToLive { get; init; } = TimeSpan.FromSeconds(120);
    public TimeSpan RenewalInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public int CommandCapacity { get; init; } = 256;
    public int MaximumLeases { get; init; } = 10_000;
    public int MaximumLeasesPerOwner { get; init; } = 128;
    public int MaximumChains { get; init; } = 8;
    public int MaximumOptions { get; init; } = 2_048;
    public int MaximumFutures { get; init; } = 256;
    public int MaximumRememberedOperations { get; init; } = 50_000;

    public TickerLeasePolicy Validate()
    {
        if (EphemeralTimeToLive < TimeSpan.FromSeconds(1) || EphemeralTimeToLive > TimeSpan.FromHours(1)
            || RenewalInterval <= TimeSpan.Zero || RenewalInterval >= EphemeralTimeToLive
            || SweepInterval <= TimeSpan.Zero || SweepInterval >= EphemeralTimeToLive
            || CommandTimeout <= TimeSpan.Zero || CommandTimeout > TimeSpan.FromMinutes(1)
            || CommandCapacity is < 1 or > 65_536 || MaximumLeases is < 1 or > 10_000
            || MaximumLeasesPerOwner is < 1 or > 128 || MaximumChains is < 1 or > 8
            || MaximumOptions is < 1 or > 2_048 || MaximumFutures is < 1 or > 256
            || MaximumRememberedOperations is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(TickerLeasePolicy), "Invalid or unbounded lease policy.");
        return this;
    }
}
