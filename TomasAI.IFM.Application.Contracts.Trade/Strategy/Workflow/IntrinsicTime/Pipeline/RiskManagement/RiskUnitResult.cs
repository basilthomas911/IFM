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
