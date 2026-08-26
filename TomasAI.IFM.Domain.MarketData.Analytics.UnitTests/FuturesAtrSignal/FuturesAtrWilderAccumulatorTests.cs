using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal;

public sealed class FuturesAtrWilderAccumulatorTests
{
    static readonly MarketSeriesIdentity Series = MarketSeriesIdentity.ForContract("ESU26");

    [Fact]
    public void TryApply_UsesOhlcGapTrueRangeAndWilderSmoothing()
    {
        FuturesAtrAccumulatorCheckpoint? checkpoint = null;
        Apply(1, 99m, 101m, 100m);
        Apply(2, 107m, 110m, 108m);
        var seeded = Apply(3, 109m, 112m, 111m);

        seeded.TrueRange.Should().Be(4m);
        seeded.AtrValue.Should().Be(16m / 3m);
        seeded.IsWarm.Should().BeTrue();

        var smoothed = Apply(4, 112m, 114m, 113m);
        smoothed.TrueRange.Should().Be(3m);
        smoothed.PreviousAtrValue.Should().Be(16m / 3m);
        smoothed.AtrValue.Should().Be(((16m / 3m) * 2m + 3m) / 3m);

        FuturesAtrWilderResult Apply(long sequence, decimal low, decimal high, decimal close)
        {
            FuturesAtrWilderAccumulator.TryApply(
                Observation(sequence, low, high, close), 3, checkpoint, out var result)
                .Should().BeTrue();
            checkpoint = result.Checkpoint;
            return result;
        }
    }

    [Fact]
    public void TryApply_RejectsDuplicateAndOlderObservation()
    {
        var first = Observation(10, 99m, 101m, 100m);
        FuturesAtrWilderAccumulator.TryApply(first, 14, null, out var accepted).Should().BeTrue();

        FuturesAtrWilderAccumulator.TryApply(first, 14, accepted.Checkpoint, out _).Should().BeFalse();
        FuturesAtrWilderAccumulator.TryApply(
            Observation(9, 98m, 100m, 99m), 14, accepted.Checkpoint, out _).Should().BeFalse();
    }

    [Fact]
    public void TryApply_ProducesPriorOnlyBaselineOnThirtyFourthObservation()
    {
        FuturesAtrAccumulatorCheckpoint? checkpoint = null;
        FuturesAtrWilderResult? result = null;
        for (var sequence = 1; sequence <= 34; sequence++)
        {
            var close = 100m + sequence;
            FuturesAtrWilderAccumulator.TryApply(
                Observation(sequence, close - 1m, close + 1m, close),
                14,
                checkpoint,
                out result).Should().BeTrue();
            checkpoint = result.Checkpoint;
        }

        result!.IsWarm.Should().BeTrue();
        result.TrueRange.Should().Be(2m);
        result.AtrValue.Should().Be(2m);
        result.PreviousAtrValue.Should().Be(2m);
        result.AtrBaseline.Should().Be(2m);
        result.AtrRatio.Should().Be(1m);
        result.Checkpoint.CompletedAtrValues.Should().HaveCount(20);
    }

    [Fact]
    public void IntradayAndDailyCommands_PersistTheSameReplayableWilderCheckpoint()
    {
        var observation = Observation(1, 99m, 101m, 100m);
        var signalId = new FuturesAtrSignalId(
            observation.ContractId,
            observation.ValueDate,
            observation.TimeFrame,
            14,
            TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
        var intraday = new GenerateFuturesAtrSignalCommand(signalId, observation.Close, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor,
                GenerateFuturesAtrSignalCommand.Verb, signalId.ToEntityId().Format())
        };
        var dailySignalId = signalId with { TimePeriod = TimeFrameType.Daily };
        var dailyObservation = observation with { TimeFrame = TimeFrameType.Daily };
        var daily = new GenerateFuturesAtrDailySignalCommand(
            dailySignalId,
            dailyObservation.Close,
            dailyObservation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor,
                GenerateFuturesAtrDailySignalCommand.Verb, dailySignalId.ToDailyEntityId().Format())
        };
        var intradayState = new FuturesAtrSignalCommandState();
        var dailyState = new FuturesAtrSignalCommandState();

        intraday.Execute(intradayState).Success.Should().BeTrue();
        daily.Execute(dailyState).Success.Should().BeTrue();

        intradayState.Events.Should().ContainSingle()
            .Which.Should().BeOfType<FuturesAtrSignalGeneratedEvent>();
        dailyState.Events.Should().ContainSingle()
            .Which.Should().BeOfType<FuturesAtrDailySignalGeneratedEvent>();
        intradayState.CalculationState.Should().BeEquivalentTo(
            dailyState.CalculationState,
            options => options.Excluding(value => value.LastObservationId));
    }

    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void DailyCommand_AcceptsEachDayBasedHorizon(TimeFrameType horizon)
    {
        var observation = DailyObservation(1, 99m, 101m, 100m);
        var signalId = new FuturesAtrSignalId(
            observation.ContractId,
            observation.ValueDate,
            horizon,
            14,
            TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
        var command = new GenerateFuturesAtrDailySignalCommand(signalId, observation.Close, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor,
                GenerateFuturesAtrDailySignalCommand.Verb, signalId.ToDailyEntityId().Format())
        };
        var state = new FuturesAtrSignalCommandState();

        command.Execute(state).Success.Should().BeTrue();

        state.Events.Should().ContainSingle();
        ((FuturesAtrDailySignalGeneratedEvent)state.Events.Single()).FuturesAtrSignal.TimePeriod
            .Should().Be(horizon);
    }

    [Fact]
    public void GeneratedEvents_MessagePackRoundTripReplayCheckpoint()
    {
        var observation = Observation(1, 99m, 101m, 100m);
        var signalId = new FuturesAtrSignalId(
            observation.ContractId,
            observation.ValueDate,
            observation.TimeFrame,
            14,
            TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
        var command = new GenerateFuturesAtrSignalCommand(signalId, observation.Close, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor,
                GenerateFuturesAtrSignalCommand.Verb, signalId.ToEntityId().Format())
        };
        var state = new FuturesAtrSignalCommandState();
        command.Execute(state).Success.Should().BeTrue();
        var generated = (FuturesAtrSignalGeneratedEvent)state.Events.Single();

        var roundTrip = MessagePackSerializer.Deserialize<FuturesAtrSignalGeneratedEvent>(
            MessagePackSerializer.Serialize(generated));

        roundTrip.CalculationState.Should().BeEquivalentTo(generated.CalculationState);
        roundTrip.FuturesAtrSignal.Should().BeEquivalentTo(generated.FuturesAtrSignal);

        var dailyObservation = DailyObservation(1, 99m, 101m, 100m);
        var dailySignalId = signalId with { TimePeriod = TimeFrameType.Weekly };
        var dailyCommand = new GenerateFuturesAtrDailySignalCommand(
            dailySignalId,
            dailyObservation.Close,
            dailyObservation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, GenerateFuturesAtrDailySignalCommand.Actor,
                GenerateFuturesAtrDailySignalCommand.Verb, dailySignalId.ToDailyEntityId().Format())
        };
        var dailyState = new FuturesAtrSignalCommandState();
        dailyCommand.Execute(dailyState).Success.Should().BeTrue();
        var dailyGenerated = (FuturesAtrDailySignalGeneratedEvent)dailyState.Events.Single();

        var dailyRoundTrip = MessagePackSerializer.Deserialize<FuturesAtrDailySignalGeneratedEvent>(
            MessagePackSerializer.Serialize(dailyGenerated));

        dailyRoundTrip.CalculationState.Should().BeEquivalentTo(dailyGenerated.CalculationState);
        dailyRoundTrip.FuturesAtrSignal.Should().BeEquivalentTo(dailyGenerated.FuturesAtrSignal);
    }

    [Fact]
    public void HistoricalWarmupRequirement_IncludesSeedAndPriorOnlyBaseline()
    {
        FuturesAtrHistoricalWarmupRequirement.GetRequiredObservationCount(14).Should().Be(34);
    }

    static FuturesTradeSessionBarReadModel Observation(
        long sequence,
        decimal low,
        decimal high,
        decimal close)
    {
        var end = new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero).AddMinutes(sequence);
        return new()
        {
            MarketSeriesIdentity = Series,
            ObservationId = FuturesTradeSessionBarId.Create(
                Series, TimeFrameType.OneMinute, end, sequence),
            ContractId = "ESU26",
            ValueDate = new(2026, 8, 26),
            TimeFrame = TimeFrameType.OneMinute,
            IntervalStartUtc = end.AddMinutes(-1),
            IntervalEndUtc = end,
            Open = close,
            High = high,
            Low = low,
            Close = close,
            Volume = 100m,
            TradeCount = 10,
            PriceVolumeSum = close * 100m,
            FirstSourceSequence = sequence,
            LastSourceSequence = sequence,
            FirstMarketEventUtc = end.AddSeconds(-30),
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

    static FuturesTradeSessionBarReadModel DailyObservation(
        long sequence,
        decimal low,
        decimal high,
        decimal close)
    {
        var source = Observation(sequence, low, high, close);
        return source with
        {
            TimeFrame = TimeFrameType.Daily,
            ObservationId = FuturesTradeSessionBarId.Create(
                Series,
                TimeFrameType.Daily,
                source.LastMarketEventUtc,
                sequence),
            IntervalStartUtc = source.LastMarketEventUtc.AddDays(-1),
            IntervalEndUtc = source.LastMarketEventUtc
        };
    }
}
