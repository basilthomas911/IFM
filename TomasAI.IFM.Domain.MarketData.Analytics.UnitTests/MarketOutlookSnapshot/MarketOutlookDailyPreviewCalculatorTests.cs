using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookDailyPreviewCalculatorTests
{
    [Fact]
    public void EveryAcceptedLivePrice_RecalculatesFromUnchangedCommittedDailyBaseline()
    {
        const string contractId = "ESZ99";
        var baseline = SeedBaseline(contractId);
        var first = Trade(contractId, 7000m, 1);
        var second = Trade(contractId, 7001m, 2);

        MarketOutlookDailyPreviewCalculator.TryCalculate(first, out var firstEma, out var firstBb)
            .Should().BeTrue();
        MarketOutlookDailyPreviewCalculator.TryCalculate(second, out var secondEma, out var secondBb)
            .Should().BeTrue();

        firstEma.IsProvisional.Should().BeTrue();
        firstBb.IsProvisional.Should().BeTrue();
        firstEma.BaselineValueDate.Should().Be(baseline.ValueDate);
        firstBb.BaselineValueDate.Should().Be(baseline.ValueDate);
        secondEma.Ema20.Should().NotBe(firstEma.Ema20);
        secondEma.Ema50.Should().NotBe(firstEma.Ema50);
        secondEma.Ema200.Should().NotBe(firstEma.Ema200);
        secondBb.StandardDeviation20.Should().NotBe(firstBb.StandardDeviation20);
        secondBb.Upper20.Should().NotBe(firstBb.Upper20);
        secondBb.Lower20.Should().NotBe(firstBb.Lower20);
        secondEma.Ema50.Should().BeApproximately(
            baseline.EmaCheckpoint.Ema50!.Value
            + (2m / 51m) * (7001m - baseline.EmaCheckpoint.Ema50.Value),
            0.000001m);
        secondEma.Ema200.Should().BeApproximately(
            baseline.EmaCheckpoint.Ema200!.Value
            + (2m / 201m) * (7001m - baseline.EmaCheckpoint.Ema200.Value),
            0.000001m);
        var referenceCloses = baseline.BbCheckpoint.Closes.Skip(1).Append(7001m).ToArray();
        var referenceMean = referenceCloses.Average();
        var referenceVariance = referenceCloses
            .Average(close => (close - referenceMean) * (close - referenceMean));
        var referenceStdDev = (decimal)Math.Sqrt((double)referenceVariance);
        secondBb.StandardDeviation20.Should().BeApproximately(referenceStdDev, 0.000001m);
        secondBb.Upper20.Should().BeApproximately(
            secondEma.Ema20!.Value + (2m * referenceStdDev), 0.000001m);
        secondBb.Lower20.Should().BeApproximately(
            secondEma.Ema20.Value - (2m * referenceStdDev), 0.000001m);
        baseline.EmaCheckpoint.Count.Should().Be(220,
            "live previews must never append a Daily observation");
        baseline.BbCheckpoint.Closes.Should().HaveCount(20);
    }

    [Fact]
    public void FinalPreview_EqualsTheSingleCompletedDailyCommitForTheSameClose()
    {
        const string contractId = "ESU00";
        const decimal finalClose = 7_250.25m;
        var baseline = SeedBaseline(contractId);
        var trade = Trade(contractId, finalClose, 77);
        MarketOutlookDailyPreviewCalculator.TryCalculate(trade, out var previewEma, out var previewBb)
            .Should().BeTrue();

        var committedObservation = CompletedObservation(
            contractId,
            trade.Price.ValueDate,
            finalClose,
            baseline.EmaCheckpoint.LastIntervalEndUtc.AddDays(1),
            77);
        var committedEma = FuturesEmaAccumulator.Apply(
            baseline.EmaCheckpoint, committedObservation).Signal!;
        var committedBb = FuturesBbAccumulator.Apply(
            baseline.BbCheckpoint, committedObservation, committedEma).Signal!;

        previewEma.Ema20.Should().Be(committedEma.Ema20);
        previewEma.Ema50.Should().Be(committedEma.Ema50);
        previewEma.Ema200.Should().Be(committedEma.Ema200);
        previewBb.StandardDeviation20.Should().Be(committedBb.StandardDeviation20);
        previewBb.Upper20.Should().Be(committedBb.Upper20);
        previewBb.Ema20Center.Should().Be(committedBb.Ema20Center);
        previewBb.Lower20.Should().Be(committedBb.Lower20);
        baseline.EmaCheckpoint.Count.Should().Be(220);
        baseline.BbCheckpoint.Closes.Should().HaveCount(20);
    }

    [Fact]
    public void SamePriceNewTrade_IsStillAValidRecalculation_WhileQuoteAndCorrectionAreIgnored()
    {
        const string contractId = "ESH00";
        SeedBaseline(contractId);
        var first = Trade(contractId, 7100m, 1);
        var samePrice = Trade(contractId, 7100m, 2);

        MarketOutlookDailyPreviewCalculator.TryCalculate(first, out var firstEma, out _)
            .Should().BeTrue();
        MarketOutlookDailyPreviewCalculator.TryCalculate(samePrice, out var secondEma, out _)
            .Should().BeTrue();
        secondEma.Ema20.Should().Be(firstEma.Ema20);

        MarketOutlookDailyPreviewCalculator.TryCalculate(
            first with { UpdateSource = FuturesMarketPriceUpdateSource.Quote }, out _, out _)
            .Should().BeFalse();
        MarketOutlookDailyPreviewCalculator.TryCalculate(
            first with
            {
                Price = first.Price with
                {
                    Trade = first.Price.Trade!.Value with
                    {
                        NormalizedTradeAction = NormalizedTradeAction.Correct
                    }
                }
            }, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void DiagnosticLineage_IsNotRequiredForAValidLatestArrivalPreview()
    {
        const string contractId = "ESN00";
        SeedBaseline(contractId);
        var source = Trade(contractId, 7_125m, 1);
        source = source with
        {
            Price = source.Price with
            {
                Trade = source.Price.Trade!.Value with
                {
                    StreamEpochId = Guid.Empty,
                    TradeOrdinal = 0
                }
            }
        };

        MarketOutlookDailyPreviewCalculator.TryCalculate(source, out var ema, out var bb)
            .Should().BeTrue();
        ema.IsProvisional.Should().BeTrue();
        bb.IsProvisional.Should().BeTrue();
        ema.LivePriceAsOfUtc.Should().Be(source.Price.Trade!.Value.EventTimestamp.ToUniversalTime());
    }

    [Fact]
    public void TenThousandLiveRecalculations_DoNotGrowOrMutateTheCommittedDailyCheckpoint()
    {
        const string contractId = "ESM00";
        var baseline = SeedBaseline(contractId);

        for (var ordinal = 1; ordinal <= 10_000; ordinal++)
        {
            var price = 7200m + ((ordinal % 17) / 10m);
            MarketOutlookDailyPreviewCalculator.TryCalculate(
                Trade(contractId, price, ordinal), out var ema, out var bb).Should().BeTrue();
            ema.IsProvisional.Should().BeTrue();
            bb.IsProvisional.Should().BeTrue();
        }

        baseline.EmaCheckpoint.Count.Should().Be(220);
        baseline.BbCheckpoint.Closes.Should().HaveCount(20);
    }

    internal static Baseline SeedBaseline(string contractId)
    {
        var series = MarketSeriesIdentity.ForContract(contractId);
        FuturesEmaAccumulatorCheckpoint? emaCheckpoint = null;
        FuturesBbAccumulatorCheckpoint? bbCheckpoint = null;
        FuturesEmaSignalReadModel ema = new();
        FuturesBbSignalReadModel bb = new();
        var start = new DateOnly(2025, 10, 1);
        for (var index = 0; index < 220; index++)
        {
            var valueDate = start.AddDays(index);
            var end = new DateTimeOffset(valueDate.ToDateTime(new TimeOnly(21, 0)), TimeSpan.Zero);
            var price = 5000m + index + ((index % 7) * 2m);
            var observation = new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = series,
                ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.Daily, end, index + 1),
                ContractId = contractId,
                ValueDate = valueDate,
                TimeFrame = TimeFrameType.Daily,
                IntervalStartUtc = end.AddDays(-1),
                IntervalEndUtc = end,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = 1,
                TradeCount = 1,
                PriceVolumeSum = price,
                FirstSourceSequence = index + 1,
                LastSourceSequence = index + 1,
                FirstMarketEventUtc = end.AddTicks(-1),
                LastMarketEventUtc = end.AddTicks(-1),
                CalculatedAtUtc = end,
                IsComplete = true,
                IsValid = true,
                CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate,
                CalculationVersion = "unit-test"
            };
            var emaResult = FuturesEmaAccumulator.Apply(emaCheckpoint, observation);
            emaCheckpoint = emaResult.Checkpoint;
            ema = emaResult.Signal!;
            var bbResult = FuturesBbAccumulator.Apply(bbCheckpoint, observation, ema);
            bbCheckpoint = bbResult.Checkpoint;
            bb = bbResult.Signal!;
        }
        RegimeDiscoverySignalCacheAdapter.Publish(ema, emaCheckpoint!);
        RegimeDiscoverySignalCacheAdapter.Publish(bb, bbCheckpoint!);
        return new(ema.Metadata.ValueDate, emaCheckpoint!, bbCheckpoint!);
    }

    internal static FuturesMarketPriceUpdatedRealtimeEvent Trade(string contractId, decimal price, long ordinal)
    {
        var valueDate = new DateOnly(2026, 8, 31);
        var timestamp = new DateTimeOffset(2026, 8, 31, 15, 0, 0, TimeSpan.Zero).AddTicks(ordinal);
        var entityId = new TickDataEntityId(contractId, valueDate, AssetTypeId.Futures);
        return new()
        {
            Subject = new(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            ReceivedOn = timestamp.UtcDateTime,
            EventSource = "unit-test",
            UpdateSource = FuturesMarketPriceUpdateSource.Trade,
            Price = new(
                contractId,
                1,
                1,
                AssetTypeId.Futures,
                valueDate,
                null,
                new FuturesMarketTradeSnapshot(
                    price,
                    1,
                    ordinal,
                    timestamp,
                    timestamp,
                    NormalizedTradeAction.New,
                    StreamEpochId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    TradeOrdinal: ordinal))
        };
    }

    static FuturesTradeSessionBarReadModel CompletedObservation(
        string contractId,
        DateOnly valueDate,
        decimal close,
        DateTimeOffset intervalEnd,
        long sourceSequence)
    {
        var series = MarketSeriesIdentity.ForContract(contractId);
        return new()
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(
                series, TimeFrameType.Daily, intervalEnd, sourceSequence),
            ContractId = contractId,
            ValueDate = valueDate,
            TimeFrame = TimeFrameType.Daily,
            IntervalStartUtc = intervalEnd.AddDays(-1),
            IntervalEndUtc = intervalEnd,
            Open = close,
            High = close,
            Low = close,
            Close = close,
            Volume = 1,
            TradeCount = 1,
            PriceVolumeSum = close,
            FirstSourceSequence = sourceSequence,
            LastSourceSequence = sourceSequence,
            FirstMarketEventUtc = intervalEnd.AddTicks(-1),
            LastMarketEventUtc = intervalEnd.AddTicks(-1),
            CalculatedAtUtc = intervalEnd,
            IsComplete = true,
            IsValid = true,
            CalculationMethod = MarketSignalCalculationMethod.ExactTrades,
            CalculationVersion = "unit-test-final-close"
        };
    }

    internal sealed record Baseline(
        DateOnly ValueDate,
        FuturesEmaAccumulatorCheckpoint EmaCheckpoint,
        FuturesBbAccumulatorCheckpoint BbCheckpoint);
}
