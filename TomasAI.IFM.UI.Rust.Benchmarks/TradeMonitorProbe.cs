using System.Diagnostics;
using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Replays identical full-evidence plans through the old projection and optimized publication paths.</summary>
static class TradeMonitorProbe
{
    public static async Task RunAsync(string output)
    {
        const int iterations = 100;
        var results = new List<object>();
        foreach (var count in new[] { 1, 100 })
        {
            var plans = Enumerable.Range(1, count + iterations).Select(i => Plan(i)).ToArray();
            var initial = plans.Take(count).Reverse().ToArray();
            foreach (var addsPlan in new[] { false, true })
            {
                var windows = Enumerable.Range(0, iterations).Select(i => addsPlan
                    ? plans.Skip(i + 1).Take(count).Reverse().ToArray() : initial).ToArray();
                using var cache = new TradePlanRowCache();
                var scope = new TradePlanHistoryScope(initial[0].Position.Id, initial[0].ValueDate, true);
                _ = cache.Rows(initial, scope);
                byte[] Legacy(StrategyTradePlanSnapshot[] window)
                    => Serialize(window.Select((plan, index) => new ContractEditorRow($"plan:{index}",
                        new() { ["kind"] = "plan", ["actionDateTimeEastern"] = TradePlanDisplay.ActionTime(plan) ?? "" }) { Data = LegacyHistory(plan) }).ToArray());
                byte[] Optimized(StrategyTradePlanSnapshot[] window) => Serialize(cache.Rows(window, scope));
                // Wire compatibility covers the complete history publication, not only individual row fields.
                if (!Legacy(initial).AsSpan().SequenceEqual(Optimized(initial))) throw new InvalidOperationException("History wire changed.");
                var baseline = Measure(windows, Legacy);
                // The baseline does not access the cache; optimized measurement starts at the initial window.
                var before = cache.Encodes;
                var optimized = Measure(windows, Optimized, warm: false);
                var encoded = cache.Encodes - before;
                var reduction = 1.0 - (double)optimized.AllocatedBytes / baseline.AllocatedBytes;
                if (reduction < .80) throw new InvalidOperationException($"Allocation target missed: {reduction:P1}");
                if (encoded != (addsPlan ? iterations : 0)) throw new InvalidOperationException($"Unexpected row encoding count: {encoded}");
                // Noise near the timer floor is bounded by an absolute tolerance as well as a relative gate.
                if (optimized.P95Microseconds > baseline.P95Microseconds * 1.10 + 100)
                    throw new InvalidOperationException("Optimized p95 refresh latency regressed.");
                results.Add(new { rows = count, scenario = addsPlan ? "new-plan" : "position-only", iterations,
                    baseline, optimized, allocationReductionPercent = reduction * 100, historyRowEncodes = encoded,
                    cacheEntries = cache.Count, cacheBytes = cache.EncodedBytes });
            }
        }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(results));
    }

    sealed record Measurement(long AllocatedBytes, double P50Microseconds, double P95Microseconds, double P99Microseconds,
        long WireBytes, int Gen0, int Gen1, int Gen2, double PauseMilliseconds);
    static Measurement Measure(StrategyTradePlanSnapshot[][] windows, Func<StrategyTradePlanSnapshot[], byte[]> encode, bool warm = true)
    {
        if (warm) _ = encode(windows[0]);
        var ticks = new double[windows.Length];
        var counts = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var pause = GC.GetTotalPauseDuration();
        var allocated = GC.GetAllocatedBytesForCurrentThread(); long wire = 0;
        for (var i = 0; i < windows.Length; i++)
        { var start = Stopwatch.GetTimestamp(); wire += encode(windows[i]).Length; ticks[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds; }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Array.Sort(ticks);
        return new(allocated, ticks[ticks.Length / 2], ticks[(int)(ticks.Length * .95)], ticks[(int)(ticks.Length * .99)],
            wire, GC.CollectionCount(0) - counts[0], GC.CollectionCount(1) - counts[1], GC.CollectionCount(2) - counts[2],
            (GC.GetTotalPauseDuration() - pause).TotalMilliseconds);
    }
    static byte[] Serialize(ContractEditorRow[] rows) => BridgeWire.Serialize(new ContractEditorPublication(1, 1, "Ready", false,
        new("trade-orders", rows, [], Context: new() { ["monitorLive"] = "True" }), []));
    static byte[] LegacyHistory(StrategyTradePlanSnapshot plan)
    {
        var map = BridgeDocument.Map(plan, true, true); map.Remove("position"); map.Remove("parameters");
        if (map.GetValueOrDefault("ironCondorTradePlanSnapshot") is Dictionary<string, object?> detail)
        { detail.Remove("position"); detail.Remove("legs"); detail.Remove("legacyLegs"); detail.Remove("strategyRiskParameterSet"); }
        return BridgeDocument.Encode(map);
    }
    static StrategyTradePlanSnapshot Plan(long revision)
    {
        var at = new DateTime(2026, 10, 21, 15, 0, 0, DateTimeKind.Utc).AddSeconds(revision);
        var position = new StrategyPositionSnapshot {
            Id = StrategyPositionId.Create(new(101, 701, 1901, 1501), TradeStrategyKind.IronCondor),
            RouteGeneration = 1, PositionSequence = revision, ValueDate = new(2026, 10, 21), AsOfUtc = at,
            Legs = Enumerable.Range(0, 4).Select(i => new StrategyPositionLeg {
                TradeLegId = new Guid(i + 1, 0, 0, new byte[8]), ContractId = $"ES-option-{i}",
                SignedQuantity = i % 2 == 0 ? -1 : 1, OpeningPrice = 12.50m, CurrentPrice = 12.50m,
                CashMultiplier = 50, Strike = 5000 + i * 50, LastPriceAtUtc = at,
                Scenario = new() { ContractId = $"ES-option-{i}", MarkPrice = 12.50m, BidPrice = 12.25m, AskPrice = 12.75m,
                    TheoreticalPrice = 12.50m, ImpliedVolatility = .20, Delta = .25, Gamma = .001, Vega = .50,
                    QuoteAtUtc = at, RiskCalculatedAtUtc = at } }).ToArray() };
        return new() { Position = position, PlanRevision = revision, ValueDate = position.ValueDate, CalculatedAtUtc = at,
            Explanation = "Full evidence presentation fixture", CurrentValue = 0,
            IronCondorTradePlanSnapshot = new() { Position = position, ContractMultiplier = 50,
                CurrentPositionValue = 0, MaxProfit = 500, VolatilityPrice = 20, ActionDateTime = at } };
    }
}
