using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
/// <summary>Price-only chart history within an inclusive server-resolved UTC window.</summary>
[MessagePackObject]
public sealed record StrategyTradePlanChartSeries(
    [property: Key(0)] DateTime StartUtc,
    [property: Key(1)] DateTime EndUtc,
    [property: Key(2)] StrategyTradePlanChartPoint[] Points);
