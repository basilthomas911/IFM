using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

public sealed class TradeRealtimeHotCacheTests
{
    [Fact]
    public void Idle_expiry_does_not_allocate_or_advance_snapshot_revision()
    {
        var clock = new Clock(); var trade = Trade();
        var cache = new TradeRealtimeHotCache(trade, new(2026, 10, 16), clock);
        foreach (var leg in trade.Legs) cache.Apply(Quote(completed: true) with { ContractId = leg.ContractId });
        for (var i = 0; i < 1000; i++) cache.Expire();
        var snapshot = cache.Read();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var changed = false;
        for (var i = 0; i < 1000; i++) changed |= cache.Expire();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.False(changed);
        Assert.Same(snapshot, cache.Read());
    }

    [Theory]
    [InlineData(int.MaxValue, int.MaxValue, true)]
    [InlineData(int.MaxValue, int.MaxValue - 1, false)]
    public void Spread_quantity_overflow_preserves_checked_sum_semantics(int shortQuantity, int longQuantity, bool overflow)
    {
        var trade = Trade(); var cache = new TradeRealtimeHotCache(trade, new(2026, 10, 16), new Clock());
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor),
            ValueDate = new(2026, 10, 16), PositionSequence = 1, RouteGeneration = 1,
            Legs = trade.Legs.Select((leg, i) => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                SignedQuantity = i == 0 ? shortQuantity : i == 1 ? longQuantity : leg.SignedQuantity,
                CurrentPrice = 1, OpeningPrice = 1 }).ToArray() };
        if (overflow) Assert.Throws<OverflowException>(() => cache.Apply(position));
        else { Assert.True(cache.Apply(position)); Assert.Null(cache.Read().Spreads[0].NetSpread); }
    }

    [Fact]
    public void Expiry_fastpath_preserves_exact_deadline_and_replacement_quote_deadline()
    {
        var clock = new Clock();
        var cache = new TradeRealtimeHotCache(Trade(), new(2026, 10, 16), clock);
        cache.Apply(Quote(completed: true));
        clock.Now = At.AddMilliseconds(999);
        Assert.False(cache.Expire());
        clock.Now = At.AddSeconds(1);
        Assert.True(cache.Expire());
        Assert.Equal("Stale", cache.Read().Legs[0].PricingStatus);
        Assert.False(cache.Expire());
        cache.Apply(Quote(tick: 2, revision: 2, completed: true) with { ObservedAtUtc = clock.Now, ValidUntilUtc = At.AddSeconds(3) });
        clock.Now = At.AddSeconds(2);
        Assert.False(cache.Expire());
        Assert.Equal("Available", cache.Read().Legs[0].PricingStatus);
        clock.Now = At.AddSeconds(3);
        Assert.True(cache.Expire());
    }

    static readonly DateTimeOffset At = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);
    static readonly Guid Generation = Guid.NewGuid();
    static EstablishedTradeDefinition Trade(int id = 1501) => new()
    {
        Id = new(101, 701, 1901, id), StrategyKind = TradeStrategyKind.IronCondor,
        Legs = Enumerable.Range(0, 4).Select(i => new TradeLegDefinition
        {
            TradeLegId = Guid.NewGuid(), ContractId = $"option-{i}", SignedQuantity = i % 2 == 0 ? -2 : 2,
            PutCall = (byte)(i < 2 ? 2 : 1), Strike = 5000 + i * 10, CashMultiplier = 50
        }).ToArray()
    };
    static FuturesOptionTickDataV2ReadModel Quote(long tick = 1, long revision = 1, bool completed = false) => new(
        "option-0", new(2026, 10, 16), tick, new(15, 0), 10, 9, 11, 3, 4, .2, 5000, -.4, .01, 12, -3, .1)
    {
        SourceGeneration = Generation, ObservationRevision = revision, GreeksAvailable = completed,
        IsPricingUpdate = completed, ObservedAtUtc = At, CalculatedAtUtc = At,
        ValidUntilUtc = At.AddSeconds(1), PricingFailure = completed ? null : "CalculationPending"
    };
    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = At;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Whole_snapshot_preserves_other_legs_and_accepts_completion_for_same_quote()
    {
        var cache = new TradeRealtimeHotCache(Trade(), new(2026, 10, 16), new Clock());
        Assert.True(cache.Apply(Quote()));
        var first = cache.Read();
        Assert.Equal(4, first.Legs.Length);
        Assert.Equal("Pending", first.Legs[0].PricingStatus);
        Assert.True(cache.Apply(Quote(revision: 2, completed: true)));
        Assert.Equal("Available", cache.Read().Legs[0].PricingStatus);
        Assert.Equal("Pending", first.Legs[0].PricingStatus);
        Assert.Equal(first.Legs[1], cache.Read().Legs[1]);
        Assert.False(cache.Apply(Quote(revision: 2, completed: true)));
        Assert.True(cache.Apply(Quote(tick: 2, revision: 3)));
        Assert.False(cache.Apply(Quote(tick: 1, revision: 4, completed: true)));
    }

    [Fact]
    public void Trade_update_never_erases_the_book_and_expiry_publishes_without_ticks()
    {
        var clock = new Clock();
        var cache = new TradeRealtimeHotCache(Trade(), new(2026, 10, 16), clock);
        cache.Apply(Quote()); cache.Apply(Quote(revision: 2, completed: true));
        Assert.True(cache.Apply(Quote(tick: 99) with { IsTradeObservation = true, BidPrice = 0, AskPrice = 0, OptionPrice = 12 }));
        Assert.Equal(9, cache.Read().Legs[0].Observation!.BidPrice);
        Assert.Equal(12, cache.Read().Legs[0].LastTradePrice);
        clock.Now = At.AddSeconds(2);
        var revision = cache.Read().SnapshotRevision;
        Assert.True(cache.Expire());
        Assert.Equal(revision + 1, cache.Read().SnapshotRevision);
        Assert.Equal("Stale", cache.Read().Legs[0].PricingStatus);
        Assert.False(cache.Expire());
    }

    [Fact]
    public void Mirror_expiry_does_not_invent_server_revisions_or_block_reconnect_catchup()
    {
        var trade = Trade(); var sourceClock = new Clock(); var displayClock = new Clock();
        var source = new TradeRealtimeHotCache(trade, new(2026, 10, 16), sourceClock);
        var mirror = new TradeRealtimeHotCache(trade, new(2026, 10, 16), displayClock);
        source.Apply(Quote(completed: true));
        Assert.True(mirror.AcceptSnapshot(source.Read()));
        displayClock.Now = At.AddSeconds(2);
        Assert.True(mirror.Expire());
        Assert.Equal(source.Read().SnapshotRevision, mirror.Read().SnapshotRevision);
        Assert.Equal(1, mirror.Read().QualityRevision);
        sourceClock.Now = displayClock.Now;
        source.Apply(Quote(tick: 2, revision: 2, completed: true) with { ObservedAtUtc = sourceClock.Now, ValidUntilUtc = At.AddSeconds(3) });
        Assert.True(mirror.AcceptSnapshot(source.Read()));
        Assert.Equal("Available", mirror.Read().Legs[0].PricingStatus);
        Assert.Equal(0, mirror.Read().QualityRevision);
        Assert.Equal(2, mirror.Read().SnapshotRevision);
    }

    [Fact]
    public void Reconnect_fences_old_generations_and_other_trade_caches_are_independent()
    {
        var a = new TradeRealtimeHotCache(Trade(), new(2026, 10, 16), new Clock());
        var b = new TradeRealtimeHotCache(Trade(1502), new(2026, 10, 16), new Clock());
        a.Apply(Quote()); b.Apply(Quote());
        Assert.True(a.Apply(Quote() with { SourceGeneration = Guid.NewGuid(), ObservedAtUtc = At.AddMilliseconds(1) }));
        Assert.False(a.Apply(Quote(tick: 900, revision: 900) with { ObservedAtUtc = At.AddSeconds(1) }));
        Assert.Equal(Generation, b.Read().Legs[0].Observation!.SourceGeneration);
        Assert.False(a.Apply(Quote() with { ContractId = "other" }));
        Assert.False(a.Apply(Quote() with { ValueDate = new(2026, 10, 15) }));
    }

    [Fact]
    public async Task Burst_writers_and_readers_observe_complete_atomic_snapshots()
    {
        var cache = new TradeRealtimeHotCache(Trade(), new(2026, 10, 16), new Clock());
        var writer = Task.Run(() => { for (var i = 1; i <= 5000; i++) cache.Apply(Quote(tick: i, revision: i)); });
        long previous = 0;
        while (!writer.IsCompleted)
        {
            var snapshot = cache.Read();
            Assert.Equal(4, snapshot.Legs.Length);
            Assert.True(snapshot.SnapshotRevision >= previous);
            previous = snapshot.SnapshotRevision;
            if (snapshot.Legs[0].Observation is { } quote) Assert.Equal(snapshot.SnapshotRevision, quote.ObservationRevision);
            await Task.Yield();
        }
        await writer;
        Assert.Equal(5000, cache.Read().SnapshotRevision);
        Assert.Equal(5000, cache.Read().Legs[0].Observation!.TickId);
    }

    [Fact]
    public void Spread_valuation_uses_signed_quantity_and_cash_multiplier_and_rejects_old_position()
    {
        var trade = Trade(); var cache = new TradeRealtimeHotCache(trade, new(2026, 10, 16), new Clock());
        var position = new StrategyPositionSnapshot
        {
            Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor), ValueDate = new(2026, 10, 16),
            PositionSequence = 2, RouteGeneration = 1,
            Legs = trade.Legs.Select((leg, i) => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                SignedQuantity = leg.SignedQuantity, CurrentPrice = 10 + i, OpeningPrice = 5 }).ToArray()
        };
        Assert.True(cache.Apply(position));
        var put = cache.Read().Spreads[0];
        Assert.Equal(1, put.NetSpread); Assert.Equal(100, put.TradeValue); Assert.Equal(100, put.TradePnl);
        Assert.False(cache.Apply(position with { PositionSequence = 1 }));
        var partial = position with { PositionSequence = 3, Legs = position.Legs.Select((leg, i) => i == 0 ? leg with { SignedQuantity = -1 } : leg).ToArray() };
        Assert.True(cache.Apply(partial));
        Assert.Null(cache.Read().Spreads[0].NetSpread);
        Assert.Equal(600, cache.Read().Spreads[0].TradeValue);
    }

    [Fact]
    public void Scenario_values_require_matching_position_and_fresh_leg_evidence()
    {
        var trade = Trade(); var clock = new Clock();
        var cache = new TradeRealtimeHotCache(trade, new(2026, 10, 16), clock);
        foreach (var leg in trade.Legs) cache.Apply(Quote(completed: true) with { ContractId = leg.ContractId });
        var position = new StrategyPositionSnapshot
        {
            Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor), ValueDate = new(2026, 10, 16),
            PositionSequence = 1, RouteGeneration = 1,
            Legs = trade.Legs.Select(leg => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                SignedQuantity = leg.SignedQuantity, CurrentPrice = 10 }).ToArray()
        };
        var plan = new StrategyTradePlanSnapshot
        {
            Position = position, ValueDate = position.ValueDate, PlanRevision = 1,
            IronCondorTradePlanSnapshot = new() { CalculationStatus = "Complete", RiskValidUntilUtc = At.AddMilliseconds(500).UtcDateTime,
                PutForwardPrice = -3, CallForwardPrice = -4, PutOTMProbability = .7, CallOTMProbability = .8,
                Legs = trade.Legs.Select(leg => new IronCondorTradePlanLegObservation { TradeLegId = leg.TradeLegId,
                    ContractId = leg.ContractId, QuoteAtUtc = At.UtcDateTime, ForwardPrice = 3 }).ToArray() }
        };
        Assert.True(cache.Apply(plan));
        Assert.Equal("Available", cache.Read().PlanStatus);
        Assert.Equal(-3, cache.Read().Spreads[0].ForwardPrice);
        Assert.NotNull(cache.Read().Legs[0].Scenario);
        clock.Now = At.AddMilliseconds(499);
        Assert.False(cache.Expire());
        clock.Now = At.AddMilliseconds(500);
        Assert.True(cache.Expire());
        Assert.Equal("Stale", cache.Read().PlanStatus);
        Assert.Equal("Available", cache.Read().Legs[0].PricingStatus);
        Assert.Null(cache.Read().Spreads[0].ForwardPrice);
        Assert.Null(cache.Read().Legs[0].Scenario);
        plan = plan with { PlanRevision = 2, IronCondorTradePlanSnapshot = plan.IronCondorTradePlanSnapshot! with {
            RiskValidUntilUtc = At.AddSeconds(1).UtcDateTime } };
        Assert.True(cache.Apply(plan));
        Assert.True(cache.Apply(Quote(tick: 2, revision: 2, completed: true) with { ObservedAtUtc = At.AddMilliseconds(1) }));
        Assert.Equal("Stale", cache.Read().PlanStatus);
        Assert.Null(cache.Read().Spreads[0].ForwardPrice);
        Assert.Null(cache.Read().Legs[0].Scenario);
        cache.Apply(position with { PositionSequence = 2 });
        Assert.False(cache.Apply(plan with { PlanRevision = 99 }));
    }

    [Fact]
    public void Managed_wire_roundtrip_retains_whole_snapshot_and_missing_values()
    {
        var cache = new TradeRealtimeHotCache(Trade(), new(2026, 10, 16), new Clock());
        cache.Apply(Quote());
        var bytes = MessagePackBinarySerializer.Shared.Serialize(cache.Read())!;
        var decoded = MessagePackBinarySerializer.Shared.Deserialize<TradeRealtimeSnapshot>(bytes)!;
        Assert.Equal(4, decoded.Legs.Length);
        Assert.Null(decoded.Legs[1].Observation);
        Assert.Null(decoded.Spreads[0].ForwardPrice);
        Assert.Equal(cache.Read().SnapshotRevision, decoded.SnapshotRevision);
    }

    [Fact]
    public void Generate_native_visual_fixture_from_managed_whole_cache_revisions()
    {
        var trade = Trade();
        var cache = new TradeRealtimeHotCache(trade, new(2026, 10, 16), new Clock());
        var position = new StrategyPositionSnapshot
        {
            Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor), ValueDate = new(2026, 10, 16),
            PositionSequence = 1, RouteGeneration = 1, AsOfUtc = At.UtcDateTime,
            Legs = trade.Legs.Select((leg, i) => new StrategyPositionLeg { TradeLegId = leg.TradeLegId,
                ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity, OpeningPrice = 8 + i, CurrentPrice = 10 + i }).ToArray()
        };
        cache.Apply(position);
        var publications = new List<object>();
        for (var revision = 1; revision <= 2; revision++)
        {
            foreach (var leg in trade.Legs)
                cache.Apply(Quote(tick: revision, revision: revision) with
                {
                    ContractId = leg.ContractId, GreeksAvailable = revision == 2,
                    PricingFailure = revision == 1 ? "CalculationPending" : null,
                    Vega = 12.3456, Rho = -.1234, Gamma = .0123, Theta = -3.4567
                });
            var rows = new[]
            {
                Row("established", trade), Row("realtime", cache.Read()), Row("position-details", position),
                Row("workspace-tab", new Dictionary<string,object> { ["key"] = trade.Id.Format(), ["title"] = "1901:1501", ["kind"] = "trade", ["active"] = true })
            };
            publications.Add(new { schema = 1, generation = revision, busy = false, status = "Completed", outcomes = Array.Empty<object>(),
                result = new { view = "trade-orders", rows, choices = new Dictionary<string, string[]>(), hasMore = false,
                    context = new Dictionary<string,string> { ["live"] = "True", ["monitorLive"] = "True", ["canLoadTrade"] = "True",
                        ["tradeId"] = "1501", ["orderId"] = "1901", ["compositionType"] = "ShortIronCondor" } } });
        }
        var path = Environment.GetEnvironmentVariable("IFM_TRADE_CACHE_FIXTURE");
        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(publications));
        Assert.Equal(2, publications.Count);
        static object Row(string kind, object value) => new { id = kind + ":0", fields = new Dictionary<string,string> { ["kind"] = kind },
            data = TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Encode(value, camelCase: true, enumNames: true).Select(value => (int)value).ToArray() };
    }
}
