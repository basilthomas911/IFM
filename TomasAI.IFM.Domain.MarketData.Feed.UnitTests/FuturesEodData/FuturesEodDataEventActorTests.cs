using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData;

public class FuturesEodDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesEodDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesEodDataEventActor : FuturesEodDataEventActor
    {
        public TestableFuturesEodDataEventActor(IActorSupervisor supervisor, 
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesEodDataEventActor> logger)
            : base(supervisor, blackboardService, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    static FuturesEodDataInsertedEvent CreateFuturesEodDataInsertedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.FuturesEodDataEntityId;
        return new FuturesEodDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesEodDataEventActor.Actor, FuturesEodDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FuturesEodData = SampleData.EodDataToday,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static VixFuturesEodDataInsertedCompleteEvent CreateVixFuturesEodDataInsertedCompleteEvent(Guid? commandId = null)
    {
        var entityId = SampleData.VixFuturesEodDataEntityId;
        return new VixFuturesEodDataInsertedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesEodDataEventActor.Actor, VixFuturesEodDataInsertedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            VixFuturesTickData = SampleData.VixTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }
}
