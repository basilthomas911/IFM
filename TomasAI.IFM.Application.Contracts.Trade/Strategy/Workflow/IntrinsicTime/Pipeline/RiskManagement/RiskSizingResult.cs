using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

[MessagePackObject]
public sealed record RiskSizingResult([property: Key(0)] int StrategyUnits,
    [property: Key(1)] CapacityRequirements? Requirements,
    [property: Key(2)] FinancialEvidenceReference? MarginEvidence,
    [property: Key(3)] ImmutableArray<string> Reasons);
