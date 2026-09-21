using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.UI.Net.Views.Trade;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class VolatilityContextHistoryUiTests
{
    [Fact]
    public async Task Read_only_context_loads_bounded_history_off_caller_thread_and_labels_point_in_time_mode()
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var snapshot = Snapshot();
        var source = new RecordingQuery(new(snapshot, 1, null, 1));
        using var control = new VolatilityContextHistoryControl();
        var request = new VolatilityContextLoadRequest(Definition(),
            new(new("simulation", snapshot.Series, snapshot.MetricPolicyVersion),
                VolatilityCalendarBucket.From(snapshot.ExchangeValueDate), snapshot.ExchangeValueDate.AddDays(-5),
                snapshot.ExchangeValueDate, null, VolatilityHistoricalMode.AsKnown, snapshot.AvailableAtUtc, 25, null),
            new(new(snapshot, 1, null, 1), VolatilityFreshnessStatus.Accepted), snapshot,
            MaximumRows: 50, MaximumPages: 2);

        await control.LoadAsync(source, request);

        source.QueryThread.Should().NotBe(callerThread);
        source.Requests.Should().ContainSingle().Which.PageSize.Should().Be(25);
        control.ModeText.Should().Be("Historical mode: As-known");
        control.CurrentText.Should().Contain("25.0000%").And.Contain("Rank: 75").And.Contain("Percentile: 80");
        control.EntrySnapshotText.Should().Contain(snapshot.SnapshotId).And.Contain(snapshot.SnapshotDigest);
        control.HistoryRowCount.Should().Be(1);
        control.Controls.Find("volatilityHistoryGrid", true).Single().Should().BeOfType<DataGridView>()
            .Which.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Unavailable_context_is_text_not_a_fabricated_numeric_zero()
    {
        using var control = new VolatilityContextHistoryControl();

        control.CurrentText.Should().Contain("Unavailable").And.NotContain("0.0000");
        control.EntrySnapshotText.Should().Contain("Unavailable");
        control.ModeText.Should().Contain("As-known").And.Contain("Restated");
    }

    static OptionIvMetricSnapshot Snapshot()
    {
        var at = new DateTimeOffset(2026, 9, 18, 20, 0, 0, TimeSpan.Zero);
        return new(1, "entry-snapshot", new string('d', 64), new("ES-ATM-30D", "method-v1"),
            "metric-v1", new(2026, 9, 18), "daily-close", .25m, VolatilityValueUnit.AnnualDecimal,
            75m, VolatilityMetricStatus.Qualified, 80m, VolatilityMetricStatus.Qualified,
            VolatilityMetricUnit.PercentagePoints0To100, .1m, .3m, 8, 0, 10, 10, 1m,
            new(2026, 9, 4), new(2026, 9, 17), ["source"], new string('s', 64), "calculator-v1",
            at.AddMinutes(-3), at.AddMinutes(-2), at.AddMinutes(-1), at);
    }

    static VolatilitySeriesDefinition Definition() => new(1, new("ES-ATM-30D", "method-v1"), "ES", "XCME", "USD",
        new("simulation", "fixture", "quotes-v1"),
        new(30, VolatilityMoneynessConvention.AtTheMoneyForward, VolatilityOptionSideSelection.CallPutCombined,
            ["EW"], VolatilitySideCombinationMethod.ArithmeticMean),
        new("European", "FuturesStyle", "DeliveryOfFuture", ["black76-v1"], "solver-v1"),
        new("mid", "quality-v1", true, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)),
        new("bracket-v1", VolatilityInterpolationMethod.LinearTotalVariance, "roll-v1", "calendar-v1",
            "America/Chicago", "daily-v1", TimeSpan.FromMinutes(5), 2),
        new("metric-v1", 10, 10, 1m, VolatilityGapPolicy.PreserveExpectedSessionGap,
            VolatilityHistoricalWindowConvention.PriorExchangeSessions, VolatilityRankRangeConvention.CurrentAndPriorWindow,
            VolatilityPercentileTieConvention.StrictlyLessThanCurrent),
        new(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null, "config-v1", "test", "SYNTHETIC", []));

    sealed class RecordingQuery(OptionIvMetricRevision revision) : IOptionVolatilityQueryApi
    {
        public int QueryThread { get; private set; }
        public List<VolatilityHistoryPageRequest> Requests { get; } = [];
        public Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(VolatilityHistoryPageRequest request,
            CancellationToken cancellationToken = default)
        {
            QueryThread = Environment.CurrentManagedThreadId;
            Requests.Add(request);
            return Task.FromResult(new VolatilityPage<OptionIvMetricRevision>([revision], null));
        }
        public Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
