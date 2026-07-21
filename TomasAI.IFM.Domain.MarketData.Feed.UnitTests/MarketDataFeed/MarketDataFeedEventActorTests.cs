using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Trade.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Event;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed;

public class MarketDataFeedEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public MarketDataFeedEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableMarketDataFeedEventActor : MarketDataFeedEventActor
    {
        public TestableMarketDataFeedEventActor(
            IActorSupervisor supervisor,
            IMarketDataApi marketDataApi,
            IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
            IBlackboardService blackboardService,
             IStatusConsoleWriter statusConsoleWriter,
            ILogger<MarketDataFeedEventActor> logger)
            : base(supervisor, marketDataApi, optionTradeLiveFeedMap, blackboardService, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    static MarketDataFeedStartedEvent CreateStartedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FeedEntityId;
        return new MarketDataFeedStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedEventActor.Actor, MarketDataFeedStartedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesContracts = SampleData.FuturesContracts,
            ValueDate = SampleData.ValueDate,
            ResetStream = false,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static MarketDataFeedStoppedEvent CreateStoppedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FeedEntityId;
        return new MarketDataFeedStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedEventActor.Actor, MarketDataFeedStoppedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            ValueDate = SampleData.ValueDate,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

    static MarketDataFeedResetEvent CreateResetEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FeedEntityId;
        return new MarketDataFeedResetEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedEventActor.Actor, MarketDataFeedResetEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesContracts = SampleData.FuturesContracts,
            ValueDate = SampleData.ValueDate,
            ResetOn = DateTime.UtcNow,
            ResetBy = "UnitTest"
        };
    }

    static MarketDataFeedStartedCompleteEvent CreateStartedCompleteEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FeedEntityId;
        return new MarketDataFeedStartedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedEventActor.Actor, MarketDataFeedStartedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 4,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesContracts = SampleData.FuturesContracts,
            ValueDate = SampleData.ValueDate,
            ResetStream = false,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static MarketDataFeedStoppedCompleteEvent CreateStoppedCompleteEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FeedEntityId;
        return new MarketDataFeedStoppedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedEventActor.Actor, MarketDataFeedStoppedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 5,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            ValueDate = SampleData.ValueDate,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

    static MarketDataFeedResetCompleteEvent CreateResetCompleteEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FeedEntityId;
        return new MarketDataFeedResetCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedEventActor.Actor, MarketDataFeedResetCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 6,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesContracts = SampleData.FuturesContracts,
            ValueDate = SampleData.ValueDate,
            ResetOn = DateTime.UtcNow,
            ResetBy = "UnitTest"
        };
    }
}
