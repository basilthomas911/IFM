using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarSignal;

/// <summary>Verifies bounded event-source reconstruction for trade-session bar publication state.</summary>
public sealed class FuturesTradeSessionBarSignalStateRepositoryTests
{
    /// <summary>Loads the last appended Published event without assuming it is the newest market interval.</summary>
    [Fact]
    public async Task LoadStateUsesLatestPublishedBarSnapshot()
    {
        const long streamId = 42;
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var intervalEnd = new DateTimeOffset(2026, 9, 14, 14, 31, 0, TimeSpan.Zero);
        var barId = FuturesTradeSessionBarId.Create(
            series,
            TimeFrameType.OneMinute,
            intervalEnd,
            12);
        var entityId = new FuturesTradeSessionBarEntityId(series, TimeFrameType.OneMinute);
        var command = new PublishFuturesTradeSessionBarCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new(
                ActorType.Command,
                PublishFuturesTradeSessionBarCommand.Actor,
                PublishFuturesTradeSessionBarCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            Bar = new FuturesTradeSessionBarReadModel
            {
                MarketSeriesIdentity = series,
                ObservationId = barId,
                ContractId = "ESZ6",
                ValueDate = new DateOnly(2026, 9, 14),
                TimeFrame = TimeFrameType.OneMinute,
                IntervalEndUtc = intervalEnd,
                IsComplete = true,
                IsValid = true
            }
        };
        var published = new FuturesTradeSessionBarPublishedEvent
        {
            Subject = new(
                ActorType.Event,
                FuturesTradeSessionBarPublishedEvent.Actor,
                FuturesTradeSessionBarPublishedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = command.CommandId,
            Bar = command.Bar
        };
        var state = new FuturesTradeSessionBarSignalCommandState();
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        stateFactory.CreateState<FuturesTradeSessionBarSignalCommandState>().Returns(state);
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        eventSource.GetEventStreamIdAsync(command.StreamId).Returns(streamId);
        eventSource.MapReduceActorEventStreamAsync<
                FuturesTradeSessionBarSignalCommandState,
                FuturesTradeSessionBarPublishedEvent>(
                streamId,
                Arg.Any<Action<IEnumerable<EventStreamReadModel>>>() )
            .Returns(call =>
            {
                call.Arg<Action<IEnumerable<EventStreamReadModel>>>()([
                    new EventStreamReadModel
                    {
                        EventVersion = 99,
                        EventTypeName = typeof(FuturesTradeSessionBarPublishedEvent).AssemblyQualifiedName!,
                        EventData = EventLogMessagePackCodec.Shared.Serialize(published)
                    }
                ]);
                return ValueTask.CompletedTask;
            });
        var repository = new FuturesTradeSessionBarSignalStateRepository(
            stateFactory,
            eventSource,
            Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<FuturesTradeSessionBarSignalCommandActor>>(),
            Substitute.For<ILogger<FuturesTradeSessionBarSignalStateRepository>>());

        var loaded = await repository.LoadStateAsync(command);

        Assert.Same(state, loaded);
        Assert.Equal(command.Subject.ThreadId, loaded.Id);
        Assert.Equal(barId, loaded.LastAppliedBarId);
        Assert.Equal(command.Bar, loaded.LastAppliedBar);
        await eventSource.Received(1).MapReduceActorEventStreamAsync<
            FuturesTradeSessionBarSignalCommandState,
            FuturesTradeSessionBarPublishedEvent>(
            streamId,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>());
        await eventSource.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamAsync<
            FuturesTradeSessionBarSignalCommandState>(default, default!);
    }
}
