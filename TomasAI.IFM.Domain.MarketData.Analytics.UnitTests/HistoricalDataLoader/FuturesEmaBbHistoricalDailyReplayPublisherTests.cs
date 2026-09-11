using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.HistoricalDataLoader;

[Collection(TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot.MarketOutlookHotCacheTestCollection.Name)]
public sealed class FuturesEmaBbHistoricalDailyReplayPublisherTests
{
    [Fact]
    public async Task OneYearReplay_WarmsEmaAndBollingerAndReconcilesTargetValueDate()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var actorService = Substitute.For<IActorService>();
        var emaCommands = new List<GenerateFuturesEmaSignalCommand>();
        var allowDurableReconciliation =
            new TaskCompletionSource<ServiceResult<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
        MarketOutlookHotCache.Shared.Clear();
        actorService.RequestAsync<GenerateFuturesEmaSignalCommand, FuturesTradeSessionBarEntityId>(
                Arg.Do<GenerateFuturesEmaSignalCommand>(emaCommands.Add))
            .Returns(_ => new ValueTask<ServiceResult<Guid>>(allowDurableReconciliation.Task));
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var firstDate = new DateOnly(2025, 1, 2);
        var observations = Enumerable.Range(0, 200)
            .Select(index => Observation(series, firstDate.AddDays(index), index + 1))
            .ToArray();
        var targetValueDate = observations[^1].ValueDate.AddDays(1);
        var publisher = new FuturesEmaBbHistoricalDailyReplayPublisher(actorService, runtime.Channel);

        var publication = publisher.PublishAsync(
            observations, targetValueDate, "ES-ACTIVE", CancellationToken.None).AsTask();
        RegimeDiscoverySignalCacheAdapter.TryGetLatestEsDailyBaseline(
                "ES-ACTIVE", out _, out _, out _, out _)
            .Should().BeTrue("stored EOD calculations must be available before durable actor reconciliation");
        allowDurableReconciliation.SetResult(new ServiceOk<Guid>(Guid.NewGuid()));
        await publication;
        await runtime.DrainAsync();

        emaCommands.Should().HaveCount(200);
        var id = new MarketOutlookEntityId("ES-ACTIVE", targetValueDate);
        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var reconcile).Should().BeTrue();
        reconcile.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.Warmup);
        reconcile.FuturesEmaSignal.Should().NotBeNull();
        reconcile.FuturesEmaSignal!.IsWarm.Should().BeTrue();
        reconcile.FuturesEmaSignal.Ema50.Should().NotBeNull();
        reconcile.FuturesEmaSignal.Ema200.Should().NotBeNull();
        reconcile.FuturesBbSignal.Should().NotBeNull();
        reconcile.FuturesBbSignal!.IsWarm.Should().BeTrue();
        reconcile.FuturesBbSignal.StandardDeviation20.Should().NotBeNull();
        reconcile.FuturesBbSignal.Upper20.Should().Be(
            reconcile.FuturesBbSignal.Ema20Center + 2m * reconcile.FuturesBbSignal.StandardDeviation20);
        reconcile.FuturesBbSignal.Lower20.Should().Be(
            reconcile.FuturesBbSignal.Ema20Center - 2m * reconcile.FuturesBbSignal.StandardDeviation20);
        RegimeDiscoverySignalCacheAdapter.TryGetLatestEsDailyBaseline(
                "ES-ACTIVE",
                out var emaBaseline,
                out var bbBaseline,
                out var committedEma,
                out var committedBb)
            .Should().BeTrue();
        emaBaseline.Should().NotBeNull();
        bbBaseline.Should().NotBeNull();
        committedEma.Should().BeEquivalentTo(reconcile.FuturesEmaSignal,
            options => options.Excluding(value => value.Metadata));
        committedBb.Should().BeEquivalentTo(reconcile.FuturesBbSignal,
            options => options.Excluding(value => value.Metadata));
        committedEma!.Metadata.MarketSeriesIdentity.Should().Be(MarketSeriesIdentity.ForContract("ES-ACTIVE"));
        committedBb!.Metadata.MarketSeriesIdentity.Should().Be(MarketSeriesIdentity.ForContract("ES-ACTIVE"));
        var regimeSnapshot = await new RegimeDiscoveryMarketSignalSnapshotProvider().CaptureAsync(
            new RegimeDiscoveryMarketSignalSnapshotRequest
            {
                MarketSeriesIdentity = MarketSeriesIdentity.ForContract("ES-ACTIVE"),
                TargetHorizon = TimeFrameType.Daily,
                Requirements =
                [
                    Requirement(RegimeDiscoverySignalMetric.Ema20),
                    Requirement(RegimeDiscoverySignalMetric.BollingerWidthRatio)
                ],
                FutureClockSkewSeconds = 1,
                SupportedSchemaVersions = [1],
                ApprovedCalculationVersions = ["1"],
                CaptureAttempts = 3
            });
        regimeSnapshot.IsSuccess.Should().BeTrue();
        regimeSnapshot.Snapshot!.Observations.Should().OnlyContain(value =>
            value.SignalKey.MarketSeriesIdentity == MarketSeriesIdentity.ForContract("ES-ACTIVE"));

        MarketOutlookHotCache.Shared.Clear();
        await publisher.PublishAsync(observations, targetValueDate, "ES-ACTIVE", CancellationToken.None);
        await runtime.DrainAsync();

        MarketOutlookHotCache.Shared.TryGetCurrent(id, out var repaired).Should().BeTrue();
        repaired.FuturesEmaSignal.Should().BeEquivalentTo(reconcile.FuturesEmaSignal);
        repaired.FuturesBbSignal.Should().BeEquivalentTo(reconcile.FuturesBbSignal);
        MarketOutlookHotCache.Shared.Clear();
    }

    static FuturesEodObservationReadModel Observation(
        MarketSeriesIdentity series,
        DateOnly date,
        long sequence)
    {
        var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(14, 30)), TimeSpan.Zero);
        var end = start.AddHours(6).AddMinutes(30);
        return new()
        {
            MarketSeriesIdentity = series,
            ContractId = "ESZ25",
            ValueDate = date,
            SessionStartUtc = start,
            SessionEndUtc = end,
            Open = 5000m + sequence,
            High = 5010m + sequence,
            Low = 4990m + sequence,
            Close = 5005m + sequence,
            Volume = 100,
            TradeCount = 10,
            PriceVolumeSum = (5005m + sequence) * 100,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, end, sequence),
            FirstSourceSequence = sequence,
            LastSourceSequence = sequence,
            FirstMarketEventUtc = start,
            LastMarketEventUtc = end.AddTicks(-1),
            SchemaVersion = 1,
            IsComplete = true,
            IsValid = true
        };
    }

    static RegimeDiscoverySignalRequirement Requirement(RegimeDiscoverySignalMetric metric) => new()
    {
        Metric = metric,
        TimeFrame = TimeFrameType.Daily,
        IsRequired = true,
        CalculationConfigurationId = $"{metric}.v1",
        MaximumAgeSeconds = int.MaxValue,
        Weight = 1m
    };
}
