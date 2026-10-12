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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This parameterless options validator reports invalid bound configuration properties, not method arguments.")]
    public DevelopmentTradingPortfolioOptions Validate()
    {
        if (string.IsNullOrWhiteSpace(PortfolioName) || string.IsNullOrWhiteSpace(ExecutionAccountReference))
            throw new ArgumentException("Development Portfolio name and execution account are required.");
        if (!string.Equals(InstrumentRoot, "ES", StringComparison.Ordinal) || !string.Equals(Currency, "USD", StringComparison.Ordinal))
            throw new ArgumentException("The v1 Development workflow supports only ES/USD.");
        if (DevelopmentCapital <= 0 || ProtectedReserve < 0 || ProtectedReserve >= DevelopmentCapital)
            throw new ArgumentException("Development capital and protected reserve are invalid.");
        if (MaximumRiskPerTrade <= 0 || MaximumAggregateRisk < MaximumRiskPerTrade || MaximumMargin <= 0 || MaximumGrossNotional <= 0 || MaximumContracts <= 0 || MaximumOpenPositions <= 0 || MaximumDrawdown <= 0)
            throw new ArgumentException("Development Portfolio limits must be positive and internally consistent.");
        if (MaximumAggregateRisk > DevelopmentCapital - ProtectedReserve || MaximumMargin > DevelopmentCapital - ProtectedReserve)
            throw new ArgumentException("Development risk or margin exceeds deployable capital.");
        return this;
    }

    public static readonly TimeFrameType[] Horizons = [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly];
}
