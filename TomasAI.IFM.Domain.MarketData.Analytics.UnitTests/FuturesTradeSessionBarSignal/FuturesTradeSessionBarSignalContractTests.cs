using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarSignal;

/// <summary>Verifies the durable futures trade-session bar publication boundary.</summary>
public sealed class FuturesTradeSessionBarSignalContractTests
{
    [Fact]
    public void AccumulatorEntityId_RoundTripsValueDateAndStableFormat()
    {
        var expected = new FuturesTradeSessionBarAccumulatorEntityId(new DateOnly(2026, 9, 2));

        var serialized = MessagePackSerializer.Serialize(expected);
        var result = MessagePackSerializer.Deserialize<FuturesTradeSessionBarAccumulatorEntityId>(serialized);

        Assert.Equal(expected, result);
        Assert.Equal("2026-09-02", result.Format());
        Assert.Equal(expected, FuturesTradeSessionBarAccumulatorEntityId.Parse(result.Format()));
        Assert.False(FuturesTradeSessionBarAccumulatorEntityId.TryParse("2026-02-30", out _));
    }

    /// <summary>Round-trips the submission identity and deterministic observation separately.</summary>
    [Fact]
    public void PublishCommand_RoundTripsCompletedBar()
    {
        var command = CreateCommand();

        var result = MessagePackSerializer.Deserialize<PublishFuturesTradeSessionBarCommand>(
            MessagePackSerializer.Serialize(command));

        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal(command.EntityId, result.EntityId);
        Assert.Equal(command.Bar, result.Bar);
        Assert.NotEqual(command.Bar.ObservationId.Value, result.CommandId);
    }

    /// <summary>Applies one deterministic publication and treats the same bar as idempotent.</summary>
    [Fact]
    public void PublisherState_AcceptsCompletedBarIdempotently()
    {
        var command = CreateCommand();
        var state = new FuturesTradeSessionBarSignalCommandState
        {
            Id = command.Subject.ThreadId
        };

        var first = command.Execute(state);
        var second = command.Execute(state);

        Assert.IsType<ServiceOk<GuidResult>>(first);
        Assert.IsType<ServiceOk<GuidResult>>(second);
        Assert.Equal(command.Bar.ObservationId, state.LastAppliedBarId);
        Assert.Single(state.Events);
    }

    /// <summary>Ignores a proven interval repeat and publishes valid distinct intervals in any order.</summary>
    [Fact]
    public void PublisherState_IgnoresKnownRepeatAndPublishesDistinctIntervals()
    {
        var first = CreateCommand();
        var state = new FuturesTradeSessionBarSignalCommandState { Id = first.Subject.ThreadId };
        Assert.True(first.Execute(state).Success);

        var recreated = CreateCommand(first.Bar with { CalculatedAtUtc = first.Bar.CalculatedAtUtc.AddSeconds(1) });
        Assert.True(recreated.Execute(state).Success);
        Assert.NotEqual(first.CommandId, recreated.CommandId);
        Assert.Single(state.Events);

        var conflictBar = first.Bar with
        {
            LastSourceSequence = first.Bar.LastSourceSequence + 1,
            ObservationId = FuturesTradeSessionBarId.Create(first.Bar.MarketSeriesIdentity,
                first.Bar.TimeFrame, first.Bar.IntervalEndUtc, first.Bar.LastSourceSequence + 1)
        };
        var conflict = CreateCommand(conflictBar).Execute(state);
        Assert.True(conflict.Success);
        Assert.Single(state.Events);
        Assert.Equal(first.Bar, state.LastAppliedBar);

        var olderEnd = first.Bar.IntervalEndUtc.AddMinutes(-1);
        var olderBar = first.Bar with
        {
            IntervalStartUtc = olderEnd.AddMinutes(-1), IntervalEndUtc = olderEnd,
            FirstMarketEventUtc = olderEnd.AddSeconds(-58),
            LastMarketEventUtc = olderEnd.AddSeconds(-1),
            ObservationId = FuturesTradeSessionBarId.Create(first.Bar.MarketSeriesIdentity,
                first.Bar.TimeFrame, olderEnd, first.Bar.LastSourceSequence)
        };
        Assert.Empty(new List<TomasAI.IFM.Shared.Validation.ValidationError>()
            .ValidatePublishBar(CreateCommand(olderBar)));
        var older = CreateCommand(olderBar).Execute(state);
        Assert.True(older.Success);
        Assert.Equal(2, state.Events.Count);
        Assert.Equal(olderBar, state.LastAppliedBar);

        var nextEnd = first.Bar.IntervalEndUtc.AddMinutes(1);
        var nextBar = first.Bar with
        {
            IntervalStartUtc = first.Bar.IntervalEndUtc, IntervalEndUtc = nextEnd,
            FirstMarketEventUtc = nextEnd.AddSeconds(-58),
            LastMarketEventUtc = nextEnd.AddSeconds(-1),
            ObservationId = FuturesTradeSessionBarId.Create(first.Bar.MarketSeriesIdentity,
                first.Bar.TimeFrame, nextEnd, first.Bar.LastSourceSequence + 1),
            LastSourceSequence = first.Bar.LastSourceSequence + 1
        };
        Assert.Empty(new List<TomasAI.IFM.Shared.Validation.ValidationError>()
            .ValidatePublishBar(CreateCommand(nextBar)));
        Assert.True(CreateCommand(nextBar).Execute(state).Success);
        Assert.Equal(nextBar, state.LastAppliedBar);
        Assert.Equal(3, state.Events.Count);

        // This interval has a later end but overlaps the last applied interval.
        var overlapEnd = nextEnd.AddSeconds(30);
        var overlapBar = nextBar with
        {
            IntervalStartUtc = nextEnd.AddSeconds(-15), IntervalEndUtc = overlapEnd,
            FirstMarketEventUtc = nextEnd.AddSeconds(-14),
            LastMarketEventUtc = overlapEnd.AddSeconds(-1),
            ObservationId = FuturesTradeSessionBarId.Create(first.Bar.MarketSeriesIdentity,
                first.Bar.TimeFrame, overlapEnd, nextBar.LastSourceSequence + 1),
            LastSourceSequence = nextBar.LastSourceSequence + 1
        };
        Assert.Empty(new List<TomasAI.IFM.Shared.Validation.ValidationError>()
            .ValidatePublishBar(CreateCommand(overlapBar)));
        Assert.True(CreateCommand(overlapBar).Execute(state).Success);
        Assert.Equal(overlapBar, state.LastAppliedBar);
        Assert.Equal(4, state.Events.Count);
    }

    /// <summary>Reports individual payload errors before any state publication.</summary>
    [Fact]
    public void PublishValidation_ReportsNullAndMalformedBarRules()
    {
        var valid = CreateCommand();
        var missing = valid with { Bar = null! };
        Assert.Contains(missingErrors(), error => error.ErrorMessage.Contains("Bar is required."));

        var malformed = valid with
        {
            Bar = valid.Bar with
            {
                IsComplete = false, IsValid = false, TradeCount = 0, Volume = 0,
                ContractId = string.Empty
            }
        };
        var errors = new List<TomasAI.IFM.Shared.Validation.ValidationError>()
            .ValidatePublishBar(malformed);
        Assert.Contains(errors, error => error.ErrorMessage.Contains("Contract Id"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("completed"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("trade evidence"));

        List<TomasAI.IFM.Shared.Validation.ValidationError> missingErrors() =>
            new List<TomasAI.IFM.Shared.Validation.ValidationError>().ValidatePublishBar(missing);
    }

    /// <summary>Copies the complete bar into both successful and failed projection terminal events.</summary>
    [Fact]
    public void PublishedEvent_CreatesConventionalTerminalEvents()
    {
        var command = CreateCommand();
        var published = new FuturesTradeSessionBarPublishedEvent
        {
            Subject = new(ActorType.Event, FuturesTradeSessionBarPublishedEvent.Actor,
                FuturesTradeSessionBarPublishedEvent.Verb, command.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = command.EntityId,
            EventId = 7,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = nameof(FuturesTradeSessionBarSignalContractTests),
            ReceivedOn = DateTime.UtcNow,
            Bar = command.Bar
        };

        var complete = published.ToCompleteEvent<
            FuturesTradeSessionBarPublishedCompleteEvent,
            FuturesTradeSessionBarEntityId>();
        var failed = published.ToFailEvent<
            FuturesTradeSessionBarPublishedFailEvent,
            FuturesTradeSessionBarEntityId>(new InvalidOperationException("projection failed"));

        var typedComplete = Assert.IsType<FuturesTradeSessionBarPublishedCompleteEvent>(complete);
        var typedFailed = Assert.IsType<FuturesTradeSessionBarPublishedFailEvent>(failed);
        Assert.Equal(command.Bar, typedComplete.Bar);
        Assert.Equal(command.CommandId, typedComplete.CommandId);
        Assert.Equal(command.CommandId, typedFailed.CommandId);
        Assert.Equal("projection failed", typedFailed.ErrorMessage);
    }

    /// <summary>Publishes the downstream Realtime bar only from the successful terminal handler.</summary>
    [Fact]
    public async Task PublishedComplete_ForwardsPersistedBarToRealtimeConsumers()
    {
        var command = CreateCommand();
        var complete = new FuturesTradeSessionBarPublishedCompleteEvent
        {
            Subject = new(ActorType.Event, FuturesTradeSessionBarPublishedEvent.Actor,
                FuturesTradeSessionBarPublishedCompleteEvent.Verb, command.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = "unit-test",
            ReceivedOn = DateTime.UtcNow,
            Bar = command.Bar
        };
        var context = Substitute.For<IFuturesTradeSessionBarSignalEventContext>();
        context.TimeProvider.Returns(new FixedTimeProvider(command.Bar.CalculatedAtUtc));
        FuturesTradeSessionBarClosedRealtimeEvent? forwarded = null;
        context.SendAsync<FuturesTradeSessionBarClosedRealtimeEvent, FuturesTradeSessionBarEntityId>(
                Arg.Do<FuturesTradeSessionBarClosedRealtimeEvent>(value => forwarded = value))
            .Returns(ValueTask.CompletedTask);

        var result = await complete.ExecuteAsync(context, Substitute.For<ILogger<FuturesTradeSessionBarSignalEventActor>>());

        Assert.True(result);
        Assert.NotNull(forwarded);
        Assert.Equal(command.Bar, forwarded.Observation);
        Assert.Equal(command.CommandId, forwarded.CommandId);
        Assert.True(forwarded.Subject.Is(
            ActorType.Realtime,
            FuturesTradeSessionBarClosedRealtimeEvent.Actor,
            FuturesTradeSessionBarClosedRealtimeEvent.Verb));
    }

    /// <summary>A failed projection terminates the bar path without a downstream realtime event.</summary>
    [Fact]
    public async Task PublishedFail_DoesNotForwardClosedBar()
    {
        var command = CreateCommand();
        var failed = new FuturesTradeSessionBarPublishedFailEvent
        {
            Subject = new(ActorType.Event, FuturesTradeSessionBarPublishedEvent.Actor,
                FuturesTradeSessionBarPublishedFailEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            ErrorMessage = "projection unavailable"
        };
        var context = Substitute.For<IFuturesTradeSessionBarSignalEventContext>();

        Assert.False(await failed.ExecuteAsync(context, Substitute.For<ILogger<FuturesTradeSessionBarSignalEventActor>>()));
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<FuturesTradeSessionBarClosedRealtimeEvent, FuturesTradeSessionBarEntityId>(default!);
    }

    static PublishFuturesTradeSessionBarCommand CreateCommand(
        FuturesTradeSessionBarReadModel? replacement = null)
    {
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var end = new DateTimeOffset(2026, 8, 25, 14, 31, 0, TimeSpan.Zero);
        var bar = new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.OneMinute, end, 12),
            ContractId = "ESU6",
            ValueDate = new(2026, 8, 25),
            TimeFrame = TimeFrameType.OneMinute,
            IntervalStartUtc = end.AddMinutes(-1),
            IntervalEndUtc = end,
            Open = 6500m,
            High = 6502m,
            Low = 6499m,
            Close = 6501m,
            Volume = 25m,
            TradeCount = 4,
            PriceVolumeSum = 162_510m,
            FirstSourceSequence = 9,
            LastSourceSequence = 12,
            FirstMarketEventUtc = end.AddSeconds(-58),
            LastMarketEventUtc = end.AddSeconds(-1),
            CalculatedAtUtc = end,
            CalculationVersion = "trade-session-bar-v1",
            IsComplete = true,
            IsValid = true,
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
            StreamEpochId = Guid.NewGuid()
        };
        var entityId = new FuturesTradeSessionBarEntityId(series, bar.TimeFrame);
        return new()
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, PublishFuturesTradeSessionBarCommand.Actor,
                PublishFuturesTradeSessionBarCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Bar = replacement ?? bar
        };
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
