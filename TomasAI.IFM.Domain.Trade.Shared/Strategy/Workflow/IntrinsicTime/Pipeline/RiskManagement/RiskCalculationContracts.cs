using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
[MessagePackObject]
public sealed record RiskUnitResult([property: Key(0)] decimal? MaximumLoss,
    [property: Key(1)] decimal ScenarioLoss,
    [property: Key(2)] decimal LossCharge,
    [property: Key(3)] decimal SettlementCash,
    [property: Key(4)] decimal GrossNotional,
    [property: Key(5)] int GrossContracts,
    [property: Key(6)] decimal Delta,
    [property: Key(7)] decimal Gamma,
    [property: Key(8)] decimal VegaPerPoint,
    [property: Key(9)] decimal ThetaPerDay,
    [property: Key(10)] int Scenarios,
    [property: Key(11)] decimal ComposerFeeReserve = 0);

/// <summary>Exact whole-order dollar quotes; quantity-specific margin must never be inferred by linear scaling.</summary>
[MessagePackObject]
public sealed record RiskQuantityFunding([property: Key(0)] int StrategyUnits,
    [property: Key(1)] decimal MarginRequirement,
    [property: Key(2)] decimal MarginFunding,
    [property: Key(3)] decimal EntryFees,
    [property: Key(4)] decimal VariationReserve,
    [property: Key(5)] FinancialEvidenceReference Evidence);

/// <summary>Engineering defaults apply independently to exactly one triggering timeframe.</summary>
[MessagePackObject]
public sealed record RiskSizingPolicy([property: Key(0)] TimeFrameType Horizon,
    [property: Key(1)] int MaximumUnits = 10,
    [property: Key(2)] decimal PerTradeRiskFraction = .01m)
{
    public static RiskSizingPolicy Default(TimeFrameType horizon)
    {
        if (horizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly)) throw new ArgumentOutOfRangeException(nameof(horizon));
        return new(horizon);
    }
}

/// <summary>Frozen coherent remaining authority. No read model can turn this calculation into a reservation.</summary>
[MessagePackObject]
public sealed record RiskSizingAuthority([property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] CatalogKey DeploymentKey,
    [property: Key(3)] string UnderlyingId,
    [property: Key(4)] decimal AvailableCash,
    [property: Key(5)] decimal RiskCapital,
    [property: Key(6)] decimal PerTradeLossBudget,
    [property: Key(7)] ImmutableArray<CapacityLimit> Limits,
    [property: Key(8)] ImmutableArray<CapacityUsed> Usage,
    [property: Key(9)] DateTime EvaluatedAtUtc,
    [property: Key(10)] DateTime ValidUntilUtc,
    [property: Key(11)] string Environment);

[MessagePackObject]
public sealed record RiskSizingResult([property: Key(0)] int StrategyUnits,
    [property: Key(1)] CapacityRequirements? Requirements,
    [property: Key(2)] FinancialEvidenceReference? MarginEvidence,
    [property: Key(3)] ImmutableArray<string> Reasons);
