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
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesTickData;

public class FuturesTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesTickDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTickDataEventActor : FuturesTickDataEventActor
    {
        public TestableFuturesTickDataEventActor(IActorSupervisor supervisor,
            IMarketDataApi marketDataApi,
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesTickDataEventActor> logger)
            : base(supervisor, marketDataApi, blackboardService, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    static FuturesTickDataInsertedEvent CreateInsertedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesTickDataId(SampleData.EsTickData.ContractId, SampleData.ValueDate, SampleData.EsTickData.TickId);
        return new FuturesTickDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickDataEventActor.Actor, FuturesTickDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.EsContract,
            TickData = SampleData.EsTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static FuturesTickDataStreamingStartedEvent CreateStreamingStartedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesTickDataStreamingId(SampleData.ValueDate);
        return new FuturesTickDataStreamingStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickDataEventActor.Actor, FuturesTickDataStreamingStartedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.EsContract,
            ValueDate = SampleData.ValueDate,
            ResetStream = false,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesTickDataStreamingStoppedEvent CreateStreamingStoppedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesTickDataStreamingId(SampleData.ValueDate);
        return new FuturesTickDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickDataEventActor.Actor, FuturesTickDataStreamingStoppedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            ContractId = SampleData.EsContract.ContractId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

}
