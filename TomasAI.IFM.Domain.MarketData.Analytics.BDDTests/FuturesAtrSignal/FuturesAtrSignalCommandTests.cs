using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesAtrSignal;

/// <summary>Describes observation-driven ATR command behavior and checkpoint persistence.</summary>
public sealed class FuturesAtrSignalCommandTests
{
    static readonly MarketSeriesIdentity Series = MarketSeriesIdentity.ForContract("ESU26");

    /// <summary>A valid closed intraday bar advances ATR and records its complete checkpoint.</summary>
    [Fact]
    public void GivenClosedIntradayBar_WhenAtrIsGenerated_ThenCheckpointIsPersisted()
    {
        var observation = Observation(1, TimeFrameType.OneMinute, 99m, 101m, 100m);
        var command = IntradayCommand(observation);
        var state = new FuturesAtrSignalCommandState();

        var result = command.Execute(state);

        result.Success.Should().BeTrue();
        var generated = state.Events.Should().ContainSingle().Subject
            .Should().BeOfType<FuturesAtrSignalGeneratedEvent>().Subject;
        generated.CalculationState.Should().NotBeNull();
        generated.CalculationState!.LastObservationId.Should().Be(observation.ObservationId);
        generated.FuturesAtrSignal.FuturesPrice.Should().Be(observation.Close);
    }

    /// <summary>A valid closed daily bar advances the day-based ATR stream with the same checkpoint model.</summary>
    [Fact]
    public void GivenClosedDailyBar_WhenAtrIsGenerated_ThenDailyCheckpointIsPersisted()
    {
        var observation = Observation(1, TimeFrameType.Daily, 99m, 101m, 100m);
        var command = DailyCommand(observation, TimeFrameType.Weekly);
        var state = new FuturesAtrSignalCommandState();

        var result = command.Execute(state);

        result.Success.Should().BeTrue();
        var generated = state.Events.Should().ContainSingle().Subject
            .Should().BeOfType<FuturesAtrDailySignalGeneratedEvent>().Subject;
        generated.CalculationState.Should().NotBeNull();
        generated.CalculationState!.LastObservationId.Should().Be(observation.ObservationId);
        generated.FuturesAtrSignal.TimePeriod.Should().Be(TimeFrameType.Weekly);
    }

    /// <summary>The same closed bar cannot advance an ATR stream twice.</summary>
    [Fact]
    public void GivenPreviouslyAppliedBar_WhenRepeated_ThenNoSecondEventIsGenerated()
    {
        var observation = Observation(1, TimeFrameType.OneMinute, 99m, 101m, 100m);
        var command = IntradayCommand(observation);
        var state = new FuturesAtrSignalCommandState();
        command.Execute(state).Success.Should().BeTrue();

        var repeated = command with { CommandId = Guid.NewGuid() };
        var result = repeated.Execute(state);

        result.Success.Should().BeTrue();
        state.Events.OfType<FuturesAtrSignalGeneratedEvent>().Should().ContainSingle();
    }

    /// <summary>Price-only legacy input is rejected because it cannot create a durable Wilder checkpoint.</summary>
    [Fact]
    public void GivenNoClosedObservation_WhenAtrIsRequested_ThenCommandFailsWithoutAnEvent()
    {
        var observation = Observation(1, TimeFrameType.OneMinute, 99m, 101m, 100m);
        var command = IntradayCommand(observation) with { Observation = null };
        var state = new FuturesAtrSignalCommandState();

        var result = command.Execute(state);

        result.Success.Should().BeFalse();
        state.Events.Should().BeEmpty();
    }

    /// <summary>A bar for another contract cannot contaminate the target ATR checkpoint.</summary>
    [Fact]
    public void GivenMismatchedObservation_WhenAtrIsRequested_ThenIdentityValidationFails()
    {
        var observation = Observation(1, TimeFrameType.OneMinute, 99m, 101m, 100m);
        var command = IntradayCommand(observation) with
        {
            Observation = observation with { ContractId = "NQU26" }
        };
        var state = new FuturesAtrSignalCommandState();

        var result = command.Execute(state);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not match the intraday ATR identity");
        state.Events.Should().BeEmpty();
    }

    static GenerateFuturesAtrSignalCommand IntradayCommand(FuturesTradeSessionBarReadModel observation)
    {
        var signalId = SignalId(observation, observation.TimeFrame);
        return new GenerateFuturesAtrSignalCommand(signalId, observation.Close, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(
                ActorType.Command,
                GenerateFuturesAtrSignalCommand.Actor,
                GenerateFuturesAtrSignalCommand.Verb,
                signalId.ToEntityId().Format())
        };
    }

    static GenerateFuturesAtrDailySignalCommand DailyCommand(
        FuturesTradeSessionBarReadModel observation,
        TimeFrameType horizon)
    {
        var signalId = SignalId(observation, horizon);
        return new GenerateFuturesAtrDailySignalCommand(signalId, observation.Close, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(
                ActorType.Command,
                GenerateFuturesAtrDailySignalCommand.Actor,
                GenerateFuturesAtrDailySignalCommand.Verb,
                signalId.ToDailyEntityId().Format())
        };
    }

    static FuturesAtrSignalId SignalId(FuturesTradeSessionBarReadModel observation, TimeFrameType timeFrame) =>
        new(
            observation.ContractId,
            observation.ValueDate,
            timeFrame,
            14,
            TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));

    static FuturesTradeSessionBarReadModel Observation(
        long sequence,
        TimeFrameType timeFrame,
        decimal low,
        decimal high,
        decimal close)
    {
        var end = new DateTimeOffset(2026, 9, 14, 14, 0, 0, TimeSpan.Zero).AddMinutes(sequence);
        return new()
        {
            MarketSeriesIdentity = Series,
            ObservationId = FuturesTradeSessionBarId.Create(Series, timeFrame, end, sequence),
            ContractId = "ESU26",
            ValueDate = new DateOnly(2026, 9, 14),
            TimeFrame = timeFrame,
            IntervalStartUtc = timeFrame == TimeFrameType.Daily ? end.AddDays(-1) : end.AddMinutes(-1),
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
            CalculationVersion = "bdd-v1",
            IsComplete = true,
            IsValid = true,
            ValidationIssues = [],
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        };
    }
}
