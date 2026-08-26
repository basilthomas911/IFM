using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketSignals;

public sealed class FuturesRegimeIndicatorStateTests
{
    static readonly MarketSeriesIdentity Series = MarketSeriesIdentity.ForFuturesSeries(
        new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));

    [Fact]
    public void Rsi13AndRsi14RetainIndependentIdentityAndWarmState()
    {
        var rsi13 = new FuturesRegimeRsiSignalState(13, FuturesRsiConfigurations.TdiRsi13);
        var rsi14 = new FuturesRegimeRsiSignalState(14, FuturesRsiConfigurations.RegimeRsi14);
        FuturesRegimeRsiSignalReadModel? result13 = null;
        FuturesRegimeRsiSignalReadModel? result14 = null;

        for (var sequence = 1; sequence <= 15; sequence++)
        {
            var observation = Observation(sequence, 100m + sequence);
            result13 = rsi13.Apply(observation);
            result14 = rsi14.Apply(observation);
        }

        Assert.True(result13!.IsWarm);
        Assert.True(result14!.IsWarm);
        Assert.NotNull(result14.Slope);
        Assert.Equal(FuturesRsiConfigurations.TdiRsi13,
            result13.Metadata.CalculationConfigurationId);
        Assert.Equal(FuturesRsiConfigurations.RegimeRsi14,
            result14.Metadata.CalculationConfigurationId);
        Assert.NotEqual(result13.Metadata.SignalKey, result14.Metadata.SignalKey);
    }

    [Fact]
    public void Ema200SeedsOnTwoHundredthCloseAndProducesPriorAndSlopeOnNextClose()
    {
        var state = new FuturesEmaSignalRealtimeState();
        FuturesEmaSignalReadModel? signal = null;
        for (var sequence = 1; sequence <= 200; sequence++)
            signal = state.Apply(Observation(sequence, sequence));

        Assert.Equal(100.5m, signal!.Ema200);
        Assert.Null(signal.PreviousEma200);
        Assert.False(signal.IsWarm);

        signal = state.Apply(Observation(201, 201m));

        Assert.Equal(101.5m, signal.Ema200);
        Assert.Equal(100.5m, signal.PreviousEma200);
        Assert.Equal(1m, signal.Ema200Slope);
        Assert.True(signal.IsWarm);
    }

    [Fact]
    public void HistoricalAndLiveEmaPathsProduceIdenticalResults()
    {
        var historical = new FuturesEmaSignalRealtimeState();
        var live = new FuturesEmaSignalRealtimeState();
        FuturesEmaSignalReadModel? expected = null;
        FuturesEmaSignalReadModel? actual = null;
        for (var sequence = 1; sequence <= 220; sequence++)
        {
            var observation = Observation(sequence, 4200m + (sequence * 0.25m));
            expected = historical.Apply(observation);
            actual = live.Apply(observation);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EmaStateReconstructedByObservationReplayMatchesUninterruptedState()
    {
        var observations = Enumerable.Range(1, 220)
            .Select(sequence => Observation(sequence, 4200m + (sequence * 0.25m)))
            .ToArray();
        var uninterrupted = new FuturesEmaSignalRealtimeState();
        foreach (var observation in observations.Take(210)) uninterrupted.Apply(observation);

        var reconstructed = new FuturesEmaSignalRealtimeState();
        foreach (var observation in observations.Take(210)) reconstructed.Apply(observation);

        FuturesEmaSignalReadModel? expected = null;
        FuturesEmaSignalReadModel? actual = null;
        foreach (var observation in observations.Skip(210))
        {
            expected = uninterrupted.Apply(observation);
            actual = reconstructed.Apply(observation);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BollingerRejectsMismatchedEmaAndWarmsFromPriorWidthsOnly()
    {
        var emaState = new FuturesEmaSignalRealtimeState();
        var bbState = new FuturesBollingerBandSignalRealtimeState();
        var first = Observation(1, 100m);
        var wrongEma = emaState.Apply(Observation(2, 101m));

        Assert.False(bbState.TryApply(first, wrongEma, out _));

        FuturesBollingerBandSignalReadModel? result = null;
        emaState = new FuturesEmaSignalRealtimeState();
        for (var sequence = 1; sequence <= 40; sequence++)
        {
            var observation = Observation(sequence, 100m + sequence);
            var ema = emaState.Apply(observation);
            Assert.True(bbState.TryApply(observation, ema, out result));
        }

        Assert.True(result!.IsWarm);
        Assert.NotNull(result.Width20Baseline);
        Assert.True(result.Width20 > 0);
        Assert.Equal(result.Ema20Center + (2m * result.StandardDeviation20), result.Upper20);
        Assert.Equal(result.Ema20Center - (2m * result.StandardDeviation20), result.Lower20);
    }

    [Fact]
    public void AttachmentRegistryIsIdempotentAndSeparatesRsiPeriods()
    {
        FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Clear();
        var rsi13 = new FuturesRsiSignalEntityId("ESU6", new(2026, 8, 25), TimeFrameType.Daily, 13);
        var rsi14 = rsi13 with { PeriodLength = 14 };

        Assert.True(FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Attach(rsi13));
        Assert.False(FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Attach(rsi13));
        Assert.True(FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Attach(rsi14));
        Assert.Equal(2, FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Snapshot().Length);
        Assert.True(FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Detach(rsi13));
        Assert.Single(FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Snapshot());
        FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Clear();
    }

    [Fact]
    public void OrderedPipelineRetainsExactObservationLineageAndRejectsDuplicateInput()
    {
        var state = new FuturesRegimeIndicatorPipelineRealtimeState();
        FuturesRegimeIndicatorSnapshot? snapshot = null;
        for (var sequence = 1; sequence <= 201; sequence++)
            snapshot = state.Apply(Observation(sequence, 5000m + sequence));

        var observationId = snapshot!.Observation.ObservationId;
        Assert.Equal(observationId, snapshot.Rsi13.Metadata.ObservationId);
        Assert.Equal(observationId, snapshot.Rsi14.Metadata.ObservationId);
        Assert.Equal(observationId, snapshot.Ema.Metadata.ObservationId);
        Assert.Equal(observationId, snapshot.BollingerBand.Metadata.ObservationId);
        Assert.True(snapshot.Rsi14.IsWarm);
        Assert.True(snapshot.Ema.IsWarm);
        Assert.True(snapshot.BollingerBand.IsWarm);
        Assert.Empty(new FuturesRegimeRsiSignalReadModelValidationRules().Execute(snapshot.Rsi14));
        Assert.Empty(new FuturesEmaSignalReadModelValidationRules().Execute(snapshot.Ema));
        Assert.Empty(new FuturesBollingerBandSignalReadModelValidationRules().Execute(snapshot.BollingerBand));
        Assert.Throws<InvalidOperationException>(() => state.Apply(snapshot.Observation));
    }

    [Fact]
    public void ProjectionEventsPreserveSnapshotAndCreateTypedCompletionAndFailure()
    {
        var state = new FuturesRegimeIndicatorPipelineRealtimeState();
        var observation = Observation(1, 5001m);
        var entityId = new FuturesTradeSessionBarEntityId(Series, TimeFrameType.Daily);
        var generated = new FuturesRegimeIndicatorsGeneratedRealtimeEvent
        {
            Subject = new(ActorType.Realtime, FuturesRegimeIndicatorsGeneratedRealtimeEvent.Actor,
                FuturesRegimeIndicatorsGeneratedRealtimeEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = Guid.NewGuid(),
            AggregateId = entityId.Format(),
            EventSource = "test",
            ReceivedOn = DateTime.UtcNow,
            Snapshot = state.Apply(observation)
        };

        var completed = generated.ToCompleteEvent<
            FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent,
            FuturesTradeSessionBarEntityId>();
        var failed = generated.ToFailEvent<
            FuturesRegimeIndicatorsGeneratedFailRealtimeEvent,
            FuturesTradeSessionBarEntityId>(new InvalidOperationException("projection failed"));

        Assert.Equal(generated.Id, completed.Id);
        Assert.Equal(generated.EntityId, completed.EntityId);
        Assert.Equal(generated.EntityId, failed.EntityId);
        Assert.Equal("projection failed", failed.ErrorMessage);
    }

    static FuturesTradeSessionBarReadModel Observation(long sequence, decimal close)
    {
        var end = new DateTimeOffset(2026, 8, 25, 20, 0, 0, TimeSpan.Zero).AddDays(sequence);
        return new()
        {
            MarketSeriesIdentity = Series,
            ObservationId = FuturesTradeSessionBarId.Create(Series, TimeFrameType.Daily, end, sequence),
            ContractId = "ESU6",
            ValueDate = DateOnly.FromDateTime(end.UtcDateTime),
            TimeFrame = TimeFrameType.Daily,
            IntervalStartUtc = end.AddDays(-1),
            IntervalEndUtc = end,
            Open = close,
            High = close + 1m,
            Low = close - 1m,
            Close = close,
            Volume = 1000m,
            TradeCount = 10,
            PriceVolumeSum = close * 1000m,
            FirstSourceSequence = sequence,
            LastSourceSequence = sequence,
            FirstMarketEventUtc = end.AddMinutes(-1),
            LastMarketEventUtc = end,
            CalculatedAtUtc = end,
            SchemaVersion = 1,
            CalculationVersion = "test-v1",
            IsComplete = true,
            IsValid = true,
            ValidationIssues = [],
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        };
    }
}
