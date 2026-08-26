using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarPublisher;

/// <summary>Verifies the durable futures trade-session bar publication boundary.</summary>
public sealed class FuturesTradeSessionBarPublisherContractTests
{
    /// <summary>Round-trips the deterministic command and complete bar payload.</summary>
    [Fact]
    public void PublishCommand_RoundTripsCompletedBar()
    {
        var command = CreateCommand();

        var result = MessagePackSerializer.Deserialize<PublishFuturesTradeSessionBarCommand>(
            MessagePackSerializer.Serialize(command));

        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal(command.EntityId, result.EntityId);
        Assert.Equal(command.Bar, result.Bar);
        Assert.Equal(command.Bar.ObservationId.Value, result.CommandId);
    }

    /// <summary>Applies one deterministic publication and treats the same bar as idempotent.</summary>
    [Fact]
    public void PublisherState_AcceptsCompletedBarIdempotently()
    {
        var command = CreateCommand();
        var state = new FuturesTradeSessionBarPublisherCommandState
        {
            Id = command.Subject.ThreadId
        };

        var first = command.Execute(state);
        var second = command.Execute(state);

        Assert.IsType<ServiceOk<GuidResult>>(first);
        Assert.IsType<ServiceOk<GuidResult>>(second);
        Assert.Equal(command.Bar.ObservationId, state.LastPublishedBarId);
        Assert.Single(state.Events);
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
            EventSource = nameof(FuturesTradeSessionBarPublisherContractTests),
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
        var context = Substitute.For<IFuturesTradeSessionBarPublisherEventContext>();
        context.TimeProvider.Returns(new FixedTimeProvider(command.Bar.CalculatedAtUtc));
        FuturesTradeSessionBarClosedRealtimeEvent? forwarded = null;
        context.SendAsync<FuturesTradeSessionBarClosedRealtimeEvent, FuturesTradeSessionBarEntityId>(
                Arg.Do<FuturesTradeSessionBarClosedRealtimeEvent>(value => forwarded = value))
            .Returns(ValueTask.CompletedTask);

        var result = await complete.ExecuteAsync(context, Substitute.For<ILogger>());

        Assert.True(result);
        Assert.NotNull(forwarded);
        Assert.Equal(command.Bar, forwarded.Observation);
        Assert.Equal(command.CommandId, forwarded.CommandId);
        Assert.True(forwarded.Subject.Is(
            ActorType.Realtime,
            FuturesTradeSessionBarClosedRealtimeEvent.Actor,
            FuturesTradeSessionBarClosedRealtimeEvent.Verb));
    }

    static PublishFuturesTradeSessionBarCommand CreateCommand()
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
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation
        };
        var entityId = new FuturesTradeSessionBarEntityId(series, bar.TimeFrame);
        return new()
        {
            CommandId = bar.ObservationId.Value,
            Subject = new(ActorType.Command, PublishFuturesTradeSessionBarCommand.Actor,
                PublishFuturesTradeSessionBarCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Bar = bar
        };
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
