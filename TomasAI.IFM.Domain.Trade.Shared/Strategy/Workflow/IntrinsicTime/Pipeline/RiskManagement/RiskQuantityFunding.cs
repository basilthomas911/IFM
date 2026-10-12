using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

/// <summary>Exact whole-order dollar quotes; quantity-specific margin must never be inferred by linear scaling.</summary>
[MessagePackObject]
public sealed record RiskQuantityFunding([property: Key(0)] int StrategyUnits,
    [property: Key(1)] decimal MarginRequirement,
    [property: Key(2)] decimal MarginFunding,
    [property: Key(3)] decimal EntryFees,
    [property: Key(4)] decimal VariationReserve,
    [property: Key(5)] FinancialEvidenceReference Evidence);
