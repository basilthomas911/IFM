using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;

/// <summary>Development-only capital and risk limits used to create the paper-trading authority.</summary>
public sealed class DevelopmentTradingPortfolioOptions
{
    public const string SectionName = "AppSettings:IntrinsicTimeStrategyWorkflow:DevelopmentPortfolio";
    public bool Enabled { get; set; }
    public string PortfolioName { get; set; } = "IFM Development Paper Portfolio";
    public string ExecutionAccountReference { get; set; } = "IFM-EMULATOR-PAPER";
    public string InstrumentRoot { get; set; } = "ES";
    public string Currency { get; set; } = "USD";
    public decimal DevelopmentCapital { get; set; } = 1_000_000m;
    public decimal ProtectedReserve { get; set; } = 100_000m;
    public decimal MaximumRiskPerTrade { get; set; } = 10_000m;
    public decimal MaximumAggregateRisk { get; set; } = 100_000m;
    public decimal MaximumMargin { get; set; } = 500_000m;
    public decimal MaximumGrossNotional { get; set; } = 5_000_000m;
    public int MaximumContracts { get; set; } = 100;
    public int MaximumOpenPositions { get; set; } = 100;
    public decimal MaximumDrawdown { get; set; } = 200_000m;

    public DevelopmentTradingPortfolioOptions Validate()
    {
        if (string.IsNullOrWhiteSpace(PortfolioName) || string.IsNullOrWhiteSpace(ExecutionAccountReference))
            throw new ArgumentException("Development Portfolio name and execution account are required.");
        if (InstrumentRoot != "ES" || Currency != "USD")
            throw new ArgumentException("The v1 Development workflow supports only ES/USD.");
        if (DevelopmentCapital <= 0 || ProtectedReserve < 0 || ProtectedReserve >= DevelopmentCapital)
            throw new ArgumentException("Development capital and protected reserve are invalid.");
        if (MaximumRiskPerTrade <= 0 || MaximumAggregateRisk < MaximumRiskPerTrade || MaximumMargin <= 0
            || MaximumGrossNotional <= 0 || MaximumContracts <= 0 || MaximumOpenPositions <= 0
            || MaximumDrawdown <= 0)
            throw new ArgumentException("Development Portfolio limits must be positive and internally consistent.");
        if (MaximumAggregateRisk > DevelopmentCapital - ProtectedReserve || MaximumMargin > DevelopmentCapital - ProtectedReserve)
            throw new ArgumentException("Development risk or margin exceeds deployable capital.");
        return this;
    }

    public static readonly TimeFrameType[] Horizons = [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly];
}

public sealed record DevelopmentTradingPortfolioReport(
    int PortfolioId,
    int PolicyId,
    IReadOnlyDictionary<TimeFrameType, int> FundIds,
    decimal DevelopmentCapital,
    int PublishedDeployments,
    bool Created,
    bool FinancialAuthorityReady);

/// <summary>Stable, reviewable identities and policy values for the Development manifest.</summary>
public static class DevelopmentTradingPortfolioDefaults
{
    public static Guid StableId(string value) => StrategyCatalogExamples.StableId("DevelopmentTradingPortfolio/" + value);
    public static Guid ConstructionId(TimeFrameType horizon) => StableId("construction/" + horizon);
    public static Guid ActivationId(int tradingYear, TimeFrameType horizon) => StableId($"activation/{tradingYear}/{horizon}");

    /// <summary>Manifest version two: Daily futures only; Weekly and Monthly use exact global option cache profiles.</summary>
    public static SelectionConstructionPolicy[] ConstructionPolicies() => DevelopmentTradingPortfolioOptions.Horizons.Select(h => new SelectionConstructionPolicy
    {
        SchemaVersion = (short)(h == TimeFrameType.Daily ? 1 : 3),
        ParameterSetId = ConstructionId(h), Version = 2, MaximumLegs = h == TimeFrameType.Daily ? 1 : 4,
        MinimumDaysToExpiry = h == TimeFrameType.Daily ? 7 : 5,
        MaximumDaysToExpiry = h == TimeFrameType.Daily ? 60 : 45,
        MinimumWingWidth = h == TimeFrameType.Daily ? 0 : 50,
        MaximumWingWidth = h == TimeFrameType.Daily ? 0 : 50,
        DeltaUnits = "UnderlyingEquivalent", MaximumDeltaTolerance = .10m,
        OptionChainCachePolicies = h == TimeFrameType.Daily ? null : OptionCacheProfiles().Select(p => new SelectionStructureOptionChainCachePolicy
        {
            StructureId = p.StrategyDefinitionId, StructureVersion = p.StrategyDefinitionVersion,
            Policy = new() { ParameterSetId = p.ParameterSetId, Version = p.Version, ConfigurationDigest = p.Hash() }
        }).ToArray()
    }).ToArray();

    /// <summary>User-approved ES profiles: 50-point widths; Iron Condor 30?45/preferred45 DTE, Verticals 5?10/preferred5 DTE.</summary>
    public static TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet[] OptionCacheProfiles() =>
        new[] { "IronCondor", "CallVertical", "PutVertical" }.Select(code =>
        {
            var id = StrategyCatalogExamples.StableId("StrategyOptionChainCache/Development" + code);
            var structure = StrategyCatalogExamples.StableId("Development" + code);
            return (code == "IronCondor"
                ? TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.IronCondor(id, structure, 1)
                : TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.VerticalSpread(id, structure, 1)) with { Enabled = true };
        }).ToArray();

    public static decimal[] CapitalAllocations(decimal total)
    {
        if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
        var share = decimal.Round(total / 3m, 2, MidpointRounding.ToZero);
        return [share, share, total - share * 2];
    }
}
