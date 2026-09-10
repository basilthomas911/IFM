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

    public static SelectionConstructionPolicy[] ConstructionPolicies() => DevelopmentTradingPortfolioOptions.Horizons.Select(h => new SelectionConstructionPolicy
    {
        SchemaVersion = 1, ParameterSetId = ConstructionId(h), Version = 1, MaximumLegs = 4,
        MinimumDaysToExpiry = h == TimeFrameType.Daily ? 7 : h == TimeFrameType.Weekly ? 14 : 21,
        MaximumDaysToExpiry = h == TimeFrameType.Daily ? 60 : h == TimeFrameType.Weekly ? 90 : 120,
        MinimumWingWidth = 5, MaximumWingWidth = 20, DeltaUnits = "UnderlyingEquivalent", MaximumDeltaTolerance = .10m
    }).ToArray();

    public static decimal[] CapitalAllocations(decimal total)
    {
        if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
        var share = decimal.Round(total / 3m, 2, MidpointRounding.ToZero);
        return [share, share, total - share * 2];
    }
}
