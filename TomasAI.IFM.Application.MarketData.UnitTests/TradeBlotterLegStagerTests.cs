using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class TradeBlotterLegStagerTests
{
    [Fact]
    public void Short_iron_condor_is_call_long_short_then_put_short_long_with_signed_delta_order()
    {
        var snapshot = Snapshot(
            ("C-5050", true, 5050m, .05), ("C-5025", true, 5025m, .16),
            ("P-4975", false, 4975m, -.16), ("P-4950", false, 4950m, -.05));

        var result = TradeBlotterLegStager.Stage(snapshot,
            new(TradeBlotterStrategy.IronCondor, TradeBlotterDirection.Short));

        Assert.Equal(new[] { "C-5050", "C-5025", "P-4975", "P-4950" }, result.Legs.Select(x => x.ContractId));
        Assert.Equal(new[] { "LL+", "SL-", "SL-", "LL+" }, result.Legs.Select(x => x.LegLabel));
        Assert.Equal(new double?[] { .05, .16, -.16, -.05 }, result.Legs.Select(x => x.Delta));
        Assert.False(result.ManualReviewRequired);
        Assert.Equal(4, result.Legs.Select(x => x.StagedLegId).Distinct().Count());
        Assert.All(result.Legs, leg => Assert.Equal(snapshot.Digest, leg.SnapshotDigest));
    }

    [Fact]
    public void Vertical_ties_are_deterministic_and_tolerance_deviation_requires_manual_review()
    {
        var snapshot = Snapshot(("C-B", true, 5020m, .14), ("C-A", true, 5010m, .18), ("C-W", true, 5060m, .05));
        var request = new TradeBlotterStagingRequest(TradeBlotterStrategy.VerticalSpread,
            TradeBlotterDirection.Short, .16, 25m, .001, 1m, TradeBlotterOptionRight.Call);

        var first = TradeBlotterLegStager.Stage(snapshot, request);
        var second = TradeBlotterLegStager.Stage(snapshot, request);

        Assert.Equal(new[] { "C-B", "C-A" }, first.Legs.Select(x => x.ContractId));
        Assert.True(first.ManualReviewRequired);
        Assert.Equal(first.Legs.Select(x => x.StagedLegId), second.Legs.Select(x => x.StagedLegId));
    }

    [Fact]
    public async Task Historical_fixture_source_preserves_as_of_snapshot_and_is_read_only_by_contract()
    {
        var snapshot = Snapshot(("C", true, 5000m, .16), ("CW", true, 5025m, .05));
        ITradeBlotterSnapshotSource source = new FixtureTradeBlotterSnapshotSource("Recorded session", snapshot);
        Assert.True(source.IsHistorical);
        Assert.Equal(snapshot.EvaluatedAtUtc, source.AsOfUtc);
        Assert.Same(snapshot, await source.CaptureAsync(default));
    }

    private static MarketCompositionSnapshot Snapshot(params (string Id, bool Call, decimal Strike, double Delta)[] values)
    {
        var instruments = values.Select(value =>
        {
            var quote = Quote(value.Id, 10m);
            var selection = new OptionSelectionValue(10, value.Delta, .2, Quote("ES-future", 5000m), quote,
                At, At, At.AddMinutes(1), "fixture", "context");
            return new CompositionInstrumentSnapshot(
                new(value.Id, quote, Context(), value.Strike, value.Call, Quote("ES-future", 5000m)) { Selection = selection }, null);
        }).ToImmutableArray();
        return new(2, Guid.Parse("8e83e079-f22c-42df-a0f2-81321ef094fd"), "ES", "scope", "Daily",
            Generation, At, At.AddMinutes(1), instruments, new string('a', 64));
    }
}
