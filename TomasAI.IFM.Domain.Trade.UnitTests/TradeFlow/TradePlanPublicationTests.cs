using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class TradePlanPublicationTests
{
    [Fact]
    public void Publication_compares_unrounded_daily_pnl_and_not_other_risk_values()
    {
        var first = Plan(1, 10m);
        Assert.True(TradePlanPublication.ShouldPublish(null, first));
        Assert.False(TradePlanPublication.ShouldPublish(first, Plan(2, 10m)));
        Assert.True(TradePlanPublication.ShouldPublish(first, Plan(3, 10.00001m)));
        Assert.True(TradePlanPublication.ShouldPublish(first, Plan(4, 0m)));
        Assert.False(TradePlanPublication.ShouldPublish(first, Plan(5, null)));
        Assert.True(TradePlanPublication.ShouldPublish(Plan(1, null), first));
        Assert.False(TradePlanPublication.ShouldPublish(Plan(1, null), Plan(2, null)));
        Assert.True(TradePlanPublication.ShouldPublish(first, Plan(2, 10m) with { ValueDate = first.ValueDate.AddDays(1) }));
    }

    [Fact]
    public void Cache_retains_every_calculation_but_tick_notifications_keep_last_pnl_publication()
    {
        var trade = new EstablishedTradeDefinition { Id = new(1, 2, 3, 4), StrategyKind = TradeStrategyKind.IronCondor };
        var first = Plan(1, 10m);
        var cache = new TradeRealtimeHotCache(trade, first.ValueDate);
        Assert.True(cache.Apply(first));
        Assert.Equal(1, cache.ReadForPublication().Plan!.PlanRevision);
        Assert.True(cache.Apply(Plan(2, 10m)));
        Assert.Equal(2, cache.Read().Plan!.PlanRevision);
        Assert.Equal(1, cache.ReadForPublication().Plan!.PlanRevision);
        Assert.True(cache.Apply(Plan(3, null)));
        Assert.Equal(3, cache.Read().Plan!.PlanRevision);
        Assert.Equal(1, cache.ReadForPublication().Plan!.PlanRevision);
        Assert.True(cache.Apply(Plan(4, 0m)));
        Assert.Equal(4, cache.ReadForPublication().Plan!.PlanRevision);
        Assert.True(cache.Apply(Plan(5, 0m)));
        Assert.Equal(5, cache.Read().Plan!.PlanRevision);
        Assert.Equal(4, cache.ReadForPublication().Plan!.PlanRevision);
    }

    static StrategyTradePlanSnapshot Plan(long revision, decimal? dailyPnl) => new()
    {
        ValueDate = new(2026, 10, 19), PlanRevision = revision,
        Position = new() { Id = StrategyPositionId.Create(new(1, 2, 3, 4), TradeStrategyKind.IronCondor),
            StrategyKind = TradeStrategyKind.IronCondor, ValueDate = new(2026, 10, 19) },
        IronCondorTradePlanSnapshot = new() { DailyPnl = dailyPnl, NetDelta = revision }
    };
}
