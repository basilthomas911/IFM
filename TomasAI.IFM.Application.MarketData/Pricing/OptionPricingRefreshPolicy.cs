namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>User-approved test defaults. Production needs an explicit reviewed policy/evidence identity.</summary>
public sealed record OptionPricingRefreshPolicy
{
    public int PriceDeltaMilliseconds { get; init; } = 250;
    public int ImpliedVolatilityMilliseconds { get; init; } = 5000;
    public int FullRiskMilliseconds { get; init; } = 1000;
    public int MaximumContracts { get; init; } = 2048;
    public int MaximumContractsPerPass { get; init; } = 16;
    public string Version { get; init; } = "OptionRefresh/TestDefaults/v1";
    public bool ProductionApproved { get; init; }
    public string? ReviewEvidenceId { get; init; }
    public bool IsProductionQualified => ProductionApproved && !string.IsNullOrWhiteSpace(ReviewEvidenceId)
        && !string.IsNullOrWhiteSpace(Version) && Version != "OptionRefresh/TestDefaults/v1";
    public void RequireProductionApproval()
    {
        Validate();
        if (!IsProductionQualified)
            throw new InvalidOperationException("Option pricing refresh settings require a reviewed production policy and evidence identity.");
    }
    public void Validate()
    {
        if (PriceDeltaMilliseconds is < 10 or > 5000 || ImpliedVolatilityMilliseconds < PriceDeltaMilliseconds
            || ImpliedVolatilityMilliseconds > 60000 || FullRiskMilliseconds is < 10 or > 60000
            || MaximumContracts is < 1 or > 2048 || MaximumContractsPerPass is < 1 or > 64
            || string.IsNullOrWhiteSpace(Version))
            throw new ArgumentException("Invalid bounded option pricing refresh policy.");
    }
}
