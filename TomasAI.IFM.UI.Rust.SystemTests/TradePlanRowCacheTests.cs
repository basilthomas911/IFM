using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class TradePlanRowCacheTests
{
    internal static StrategyTradePlanSnapshot Plan(long revision, int trade = 1501, long generation = 1) => new()
    {
        Position = new() { Id = StrategyPositionId.Create(new(101, 701, 1901, trade), TradeStrategyKind.IronCondor), RouteGeneration = generation },
        ValueDate = new(2026, 10, 21), PlanRevision = revision,
        CalculatedAtUtc = new DateTime(2026, 10, 21, 15, 0, 0, DateTimeKind.Utc).AddSeconds(revision),
        Explanation = $"Revision {revision}", IronCondorTradePlanSnapshot = new() { CurrentPositionValue = -542.50m }
    };
    static TradePlanHistoryScope Scope(StrategyTradePlanSnapshot plan, bool live = true) => new(plan.Position.Id, plan.ValueDate, live);

    [Fact]
    public void Price_refresh_reuses_rows_and_one_new_plan_encodes_once_even_when_indexes_shift()
    {
        using var cache = new TradePlanRowCache();
        var plans = Enumerable.Range(1, 100).Select(i => Plan(i)).Reverse().ToArray();
        var first = cache.Rows(plans, Scope(plans[0]));
        for (var refresh = 0; refresh < 1000; refresh++) Assert.Same(first, cache.Rows(plans, Scope(plans[0])));
        Assert.Equal(100, cache.Encodes);
        var next = new[] { Plan(101) }.Concat(plans.Take(99)).ToArray();
        var rows = cache.Rows(next, Scope(next[0]));
        Assert.Equal(101, cache.Encodes); Assert.Equal(100, cache.Count);
        Assert.Same(first[0].Data, rows[1].Data); Assert.Same(first[0].Fields, rows[1].Fields);
        Assert.Equal("plan:1", rows[1].Id); Assert.Equal("plan:0", first[0].Id);
    }

    [Fact]
    public void Replaced_identity_dates_generations_modes_and_trades_cannot_reuse_stale_payloads()
    {
        using var cache = new TradePlanRowCache();
        var first = Plan(1); var initial = cache.Rows([first], Scope(first))[0];
        var replacement = first with { Explanation = "corrected evidence" };
        var replaced = cache.Rows([replacement], Scope(first))[0];
        Assert.NotSame(initial.Data, replaced.Data);
        Assert.Equal(TradePlanDisplay.EncodeHistory(replacement), replaced.Data);
        foreach (var plan in new[] { Plan(1, generation: 2), Plan(1, trade: 1502), first with { ValueDate = first.ValueDate.AddDays(1) } })
            Assert.Equal(TradePlanDisplay.EncodeHistory(plan), cache.Rows([plan], Scope(plan))[0].Data);
        var before = cache.Encodes;
        cache.Rows([first], Scope(first, false));
        Assert.Equal(before + 1, cache.Encodes);
        cache.Clear(first.Position.Id);
        Assert.True(cache.Count <= 1);
    }

    [Fact]
    public void Eviction_and_oversized_rows_bound_cache_without_dropping_displayed_rows()
    {
        var plans = Enumerable.Range(1, 100).Select(i => Plan(i)).ToArray();
        using var cache = new TradePlanRowCache(maximumBytes: 1, maximumEntries: 2);
        Assert.Equal(100, cache.Rows(plans, Scope(plans[0])).Length);
        Assert.Equal(0, cache.Count); Assert.Equal(0, cache.EncodedBytes);
        using var bounded = new TradePlanRowCache(maximumEntries: 3);
        for (var trade = 1; trade <= 100; trade++)
        { var plan = Plan(1, trade: trade); bounded.Rows([plan], Scope(plan)); Assert.True(bounded.Count <= 3); }
        bounded.Dispose(); Assert.Equal(0, bounded.Count); Assert.Equal(0, bounded.EncodedBytes);
        Assert.Throws<ObjectDisposedException>(() => bounded.Rows(plans, Scope(plans[0])));
    }

    [Fact]
    public void Unchanged_sections_reuse_owned_rows_and_revision_changes_refresh_mutable_state()
    {
        using var sections = new TradeMonitorRowSections();
        var source = new object(); var encodes = 0;
        ContractEditorRow[] Encode(int value) { encodes++; return [new(value.ToString(), [])]; }
        var first = sections.Rows(TradeMonitorSection.PositionDetails, source, 1, 1, Encode);
        Assert.Same(first, sections.Rows(TradeMonitorSection.PositionDetails, source, 1, 1, Encode));
        Assert.NotSame(first, sections.Rows(TradeMonitorSection.PositionDetails, source, 2, 2, Encode));
        Assert.Equal(2, encodes);
    }
}
