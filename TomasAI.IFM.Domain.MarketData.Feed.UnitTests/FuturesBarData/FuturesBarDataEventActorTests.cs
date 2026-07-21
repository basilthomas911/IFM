using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData;

public class FuturesBarDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesBarDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesBarDataEventActor : FuturesBarDataEventActor
    {
        public TestableFuturesBarDataEventActor(IActorSupervisor supervisor, 
            IFuturesBarDataTimer futuresBarTimer,
            IStatusConsoleWriter statusConsoleWriter, 
            ILogger<FuturesBarDataEventActor> logger)
            : base(supervisor, futuresBarTimer, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    static FuturesBarDataStreamingStartedEvent CreateStreamingStartedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        return new FuturesBarDataStreamingStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesBarDataEventActor.Actor, FuturesBarDataStreamingStartedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contracts = SampleData.FuturesContracts,
            ValueDate = SampleData.ValueDate,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesBarDataStreamingStoppedEvent CreateStreamingStoppedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesBarDataStreamingId(SampleData.ValueDate);
        return new FuturesBarDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesBarDataEventActor.Actor, FuturesBarDataStreamingStoppedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

}
