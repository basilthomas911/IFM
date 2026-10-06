using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.SignalOwnership;

/// <summary>Qualifies independent event-sourced RSI, EMA, and Bollinger calculation ownership.</summary>
public sealed class FuturesSignalAccumulatorTests
{
    static readonly MarketSeriesIdentity Series = MarketSeriesIdentity.ForFuturesSeries(
        new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));

    [Fact]
    public void WilderRsiSeedsAfterExactlyPeriodPriceChangesAndThenWarmsSlope()
    {
        FuturesRsiAccumulatorCheckpoint? checkpoint = null;
        FuturesRsiWilderResult? result = null;
        for (var sequence = 1; sequence <= 15; sequence++)
        {
            result = FuturesRsiWilderAccumulator.Apply(checkpoint, Observation(sequence, 100m + sequence), 13);
            checkpoint = result.Checkpoint;
        }

        Assert.Equal(100d, checkpoint!.CurrentRsi);
        Assert.Equal(100d, result!.PreviousRsi);
        Assert.Equal(0d, result.Slope);
        Assert.True(result.IsWarm);
    }

    [Fact]
    public void PrewarmRsiIsExplicitlyInvalidAndCannotReplaceTheWarmHotValue()
    {
        var observation = Observation(1, 101m);
        var result = FuturesRsiWilderAccumulator.Apply(null, observation, 13);

        var signal = FuturesRsiWilderSignalFactory.Create(observation, 13, result);

        Assert.False(signal.IsWarm);
        Assert.Equal(-1d, signal.RSI);
        Assert.False(signal.Metadata!.IsValid);
        Assert.Contains(MarketSignalValidationIssue.InvalidCalculation,
            signal.Metadata.ValidationIssues);
    }

    [Fact]
    public void RsiConfigurationIdentitySeparatesTdi13FromRegime14()
    {
        FuturesRsiAccumulatorCheckpoint? rsi13 = null;
        FuturesRsiAccumulatorCheckpoint? rsi14 = null;
        FuturesRsiSignalReadModel? signal13 = null;
        FuturesRsiSignalReadModel? signal14 = null;
        for (var sequence = 1; sequence <= 16; sequence++)
        {
            var observation = Observation(sequence, 100m + sequence);
            var result13 = FuturesRsiWilderAccumulator.Apply(rsi13, observation, 13);
            var result14 = FuturesRsiWilderAccumulator.Apply(rsi14, observation, 14);
            rsi13 = result13.Checkpoint;
            rsi14 = result14.Checkpoint;
            signal13 = FuturesRsiWilderSignalFactory.Create(observation, 13, result13);
            signal14 = FuturesRsiWilderSignalFactory.Create(observation, 14, result14);
        }

        Assert.Equal(FuturesRsiConfigurations.TdiRsi13, signal13!.Metadata.CalculationConfigurationId);
        Assert.Equal(FuturesRsiConfigurations.RegimeRsi14, signal14!.Metadata.CalculationConfigurationId);
        Assert.NotEqual(signal13.Metadata.SignalKey, signal14.Metadata.SignalKey);
        Assert.True(signal13.IsWarm);
        Assert.True(signal14.IsWarm);
    }

    [Fact]
    public void RsiIgnoresDuplicateAndStaleObservationsWithoutThrowing()
    {
        var first = Observation(2, 102m);
        var checkpoint = FuturesRsiWilderAccumulator.Apply(null, first, 14).Checkpoint;

        var duplicate = FuturesRsiWilderAccumulator.Apply(checkpoint, first, 14);
        var stale = FuturesRsiWilderAccumulator.Apply(checkpoint, Observation(1, 101m), 14);

        Assert.Equal(MarketObservationApplicationDisposition.Duplicate, duplicate.Disposition);
        Assert.Equal(MarketObservationApplicationDisposition.Stale, stale.Disposition);
        Assert.Same(checkpoint, duplicate.Checkpoint);
        Assert.Same(checkpoint, stale.Checkpoint);
        Assert.False(duplicate.IsApplied);
        Assert.False(stale.IsApplied);
    }

    [Fact]
    public void LaterBarFromNewStreamEpochAdvancesAllDurableAccumulatorsWithLowerSequence()
    {
        var oldEpoch = Guid.NewGuid();
        var newEpoch = Guid.NewGuid();
        var first = Observation(10_000, 5000m, oldEpoch, 1);
        var resumed = Observation(1, 5001m, newEpoch, 2);

        var emaFirst = FuturesEmaAccumulator.Apply(null, first);
        var emaResumed = FuturesEmaAccumulator.Apply(emaFirst.FuturesEmaCheckpoint, resumed);
        Assert.True(emaResumed.IsApplied);
        Assert.Equal(2, emaResumed.FuturesEmaCheckpoint.Count);
        Assert.Equal(newEpoch, emaResumed.FuturesEmaCheckpoint.LastStreamEpochId);
        Assert.Equal(resumed.IntervalEndUtc, emaResumed.FuturesEmaCheckpoint.LastIntervalEndUtc);
        Assert.Equal(newEpoch, emaResumed.FuturesEmaSignal!.Metadata.StreamEpochId);

        var bbFirst = FuturesBbAccumulator.Apply(null, first, emaFirst.FuturesEmaSignal!);
        var bbResumed = FuturesBbAccumulator.Apply(
            bbFirst.FuturesBbCheckpoint, resumed, emaResumed.FuturesEmaSignal!);
        Assert.True(bbResumed.IsApplied);
        Assert.Equal(2, bbResumed.FuturesBbCheckpoint.Closes.Length);
        Assert.Equal(newEpoch, bbResumed.FuturesBbCheckpoint.LastStreamEpochId);

        var rsiFirst = FuturesRsiWilderAccumulator.Apply(null, first, 14);
        var rsiResumed = FuturesRsiWilderAccumulator.Apply(rsiFirst.Checkpoint, resumed, 14);
        Assert.True(rsiResumed.IsApplied);
        Assert.Equal(1, rsiResumed.Checkpoint.ChangeCount);
        Assert.Equal(newEpoch, rsiResumed.Checkpoint.LastStreamEpochId);
    }

    [Fact]
    public void EmaAndBollingerIgnoreDuplicateAndOlderIntervalsWithoutThrowing()
    {
        var newest = Observation(20, 5020m);
        var older = Observation(19, 5019m);
        var emaApplied = FuturesEmaAccumulator.Apply(null, newest);
        var emaDuplicate = FuturesEmaAccumulator.Apply(emaApplied.FuturesEmaCheckpoint, newest);
        var emaStale = FuturesEmaAccumulator.Apply(emaApplied.FuturesEmaCheckpoint, older);

        Assert.Equal(MarketObservationApplicationDisposition.Duplicate, emaDuplicate.Disposition);
        Assert.Equal(MarketObservationApplicationDisposition.Stale, emaStale.Disposition);
        Assert.Null(emaDuplicate.FuturesEmaSignal);
        Assert.Null(emaStale.FuturesEmaSignal);

        var bbApplied = FuturesBbAccumulator.Apply(null, newest, emaApplied.FuturesEmaSignal!);
        var bbDuplicate = FuturesBbAccumulator.Apply(bbApplied.FuturesBbCheckpoint, newest, emaApplied.FuturesEmaSignal!);
        var olderEma = FuturesEmaAccumulator.Apply(null, older).FuturesEmaSignal!;
        var bbStale = FuturesBbAccumulator.Apply(bbApplied.FuturesBbCheckpoint, older, olderEma);

        Assert.Equal(MarketObservationApplicationDisposition.Duplicate, bbDuplicate.Disposition);
        Assert.Equal(MarketObservationApplicationDisposition.Stale, bbStale.Disposition);
        Assert.Null(bbDuplicate.FuturesBbSignal);
        Assert.Null(bbStale.FuturesBbSignal);
    }

    [Fact]
    public void DuplicateEmaCommandReturnsSuccessWithoutAppendingAnotherEvent()
    {
        var observation = Observation(1, 5001m);
        var entityId = new FuturesTradeSessionBarEntityId(Series, TimeFrameType.Daily);
        var command = new GenerateFuturesEmaSignalCommand
        {
            CommandId = observation.ObservationId.Value,
            Subject = new(ActorType.Command, GenerateFuturesEmaSignalCommand.Actor,
                GenerateFuturesEmaSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Observation = observation
        };
        var state = new FuturesEmaSignalCommandState();

        Assert.True(command.Execute(state).Success);
        Assert.True(command.Execute(state).Success);

        Assert.Single(state.Events);
        Assert.Equal(1, state.FuturesEmaCheckpoint!.Count);
    }

    [Fact]
    public void Ema200SeedsOnCloseTwoHundredAndSuppliesPriorAndSlopeOnTwoHundredOne()
    {
        FuturesEmaAccumulatorCheckpoint? checkpoint = null;
        FuturesEmaAccumulatorResult? result = null;
        for (var sequence = 1; sequence <= 200; sequence++)
        {
            result = FuturesEmaAccumulator.Apply(checkpoint, Observation(sequence, sequence));
            checkpoint = result.FuturesEmaCheckpoint;
        }
        Assert.Equal(100.5m, result!.FuturesEmaSignal.Ema200);
        Assert.Null(result.FuturesEmaSignal.PreviousEma200);
        Assert.True(result.FuturesEmaSignal.IsWarm);

        result = FuturesEmaAccumulator.Apply(checkpoint, Observation(201, 201m));
        Assert.Equal(101.5m, result.FuturesEmaSignal.Ema200);
        Assert.Equal(100.5m, result.FuturesEmaSignal.PreviousEma200);
        Assert.Equal(1m, result.FuturesEmaSignal.Ema200Slope);
        Assert.True(result.FuturesEmaSignal.IsWarm);
    }

    [Fact]
    public void EmaCheckpointResumeMatchesUninterruptedCalculation()
    {
        FuturesEmaAccumulatorCheckpoint? uninterrupted = null;
        FuturesEmaAccumulatorCheckpoint? restored = null;
        FuturesEmaSignalReadModel? expected = null;
        FuturesEmaSignalReadModel? actual = null;
        for (var sequence = 1; sequence <= 220; sequence++)
        {
            var observation = Observation(sequence, 4200m + sequence * .25m);
            var a = FuturesEmaAccumulator.Apply(uninterrupted, observation);
            uninterrupted = a.FuturesEmaCheckpoint;
            expected = a.FuturesEmaSignal;
            if (sequence <= 210)
                restored = a.FuturesEmaCheckpoint with { };
            else
            {
                var b = FuturesEmaAccumulator.Apply(restored, observation);
                restored = b.FuturesEmaCheckpoint;
                actual = b.FuturesEmaSignal;
            }
        }
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BollingerRejectsMismatchedEmaAndUsesPriorWidthsForBaseline()
    {
        var first = Observation(1, 101m);
        var wrong = FuturesEmaAccumulator.Apply(null, Observation(2, 102m)).FuturesEmaSignal;
        Assert.Throws<InvalidOperationException>(() => FuturesBbAccumulator.Apply(null, first, wrong));

        FuturesEmaAccumulatorCheckpoint? emaState = null;
        FuturesBbAccumulatorCheckpoint? bbState = null;
        FuturesBbSignalReadModel? signal = null;
        for (var sequence = 1; sequence <= 40; sequence++)
        {
            var observation = Observation(sequence, 100m + sequence);
            var ema = FuturesEmaAccumulator.Apply(emaState, observation);
            emaState = ema.FuturesEmaCheckpoint;
            var bb = FuturesBbAccumulator.Apply(bbState, observation, ema.FuturesEmaSignal);
            bbState = bb.FuturesBbCheckpoint;
            signal = bb.FuturesBbSignal;
        }
        Assert.True(signal!.IsWarm);
        Assert.NotNull(signal.Width20Baseline);
        Assert.Equal(signal.Ema20Center + 2m * signal.StandardDeviation20, signal.Upper20);
        Assert.Equal(signal.Ema20Center - 2m * signal.StandardDeviation20, signal.Lower20);
    }

    [Fact]
    public void GeneratedEventsPreserveCheckpointsInCompletionEvents()
    {
        var observation = Observation(1, 5001m);
        var entityId = new FuturesTradeSessionBarEntityId(Series, TimeFrameType.Daily);
        var result = FuturesEmaAccumulator.Apply(null, observation);
        var generated = new FuturesEmaSignalGeneratedEvent
        {
            Subject = new(ActorType.Event, FuturesEmaSignalGeneratedEvent.Actor,
                FuturesEmaSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            FuturesEmaSignal = result.FuturesEmaSignal,
            Observation = observation,
            FuturesEmaCheckpoint = result.FuturesEmaCheckpoint
        };

        var completed = (FuturesEmaSignalGeneratedCompleteEvent)generated.ToCompleteEvent<
            FuturesEmaSignalGeneratedCompleteEvent, FuturesTradeSessionBarEntityId>();
        Assert.Equal(generated.FuturesEmaCheckpoint, completed.FuturesEmaCheckpoint);
        Assert.Equal(observation.ObservationId, completed.FuturesEmaSignal.Metadata.ObservationId);
    }

    [Fact]
    public void EmaRealtimeActorDeclaresNoCalculationState()
    {
        var fields = typeof(FuturesEmaSignalRealtimeActor).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        Assert.DoesNotContain(fields, field =>
            field.FieldType == typeof(FuturesEmaAccumulatorCheckpoint)
            || field.FieldType == typeof(FuturesEmaSignalReadModel));
    }

    static FuturesTradeSessionBarReadModel Observation(
        long sequence,
        decimal close,
        Guid streamEpochId = default,
        int? intervalDay = null)
    {
        var end = new DateTimeOffset(2026, 1, 1, 20, 0, 0, TimeSpan.Zero)
            .AddDays(intervalDay ?? checked((int)sequence));
        return new()
        {
            MarketSeriesIdentity = Series,
            ObservationId = FuturesTradeSessionBarId.Create(Series, TimeFrameType.Daily, end, sequence),
            ContractId = "ESH6",
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
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
            StreamEpochId = streamEpochId
        };
    }
}
