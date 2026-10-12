using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

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
