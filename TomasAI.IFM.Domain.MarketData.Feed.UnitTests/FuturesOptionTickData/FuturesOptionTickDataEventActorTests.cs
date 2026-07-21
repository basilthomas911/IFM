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
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionTickData;

public class FuturesOptionTickDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionTickDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesOptionTickDataEventActor : FuturesOptionTickDataEventActor
    {
        public TestableFuturesOptionTickDataEventActor(
            IActorSupervisor supervisor,
             IMarketDataApi marketDataApi,
            IMarketDataSnapshotApi marketDataSnapshotApi,
            IBlackboardService blackboardService,
            IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesOptionTickDataEventActor> logger)
            : base(supervisor, marketDataApi, marketDataSnapshotApi, blackboardService, optionTradeLiveFeedMap, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    static FuturesOptionTickDataInsertedEvent CreateInsertedEvent(Guid? commandId = null)
    {
        var entityId = new FuturesOptionTickEntityId(SampleData.EsOptionTickData.ContractId, SampleData.ValueDate);
        return new FuturesOptionTickDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickDataInsertedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.EsContract,
            TickData = SampleData.EsOptionTickData,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "UnitTest"
        };
    }

    static FuturesOptionTickDataStreamingStartedEvent CreateStreamingStartedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.OptionTickStreamingFeedId;
        return new FuturesOptionTickDataStreamingStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickDataStreamingStartedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            Contract = SampleData.FuturesOptionContracts[0],
            BaseContract = SampleData.EsContract,
            ValueDate = SampleData.ValueDate,
            MaturityDate = SampleData.OptionMaturityDate,
            RiskFreeRate = SampleData.RiskFreeRate,
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesOptionTickDataStreamingStoppedEvent CreateStreamingStoppedEvent(Guid? commandId = null)
    {
        var entityId = SampleData.OptionTickStreamingFeedId;
        return new FuturesOptionTickDataStreamingStoppedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickDataStreamingStoppedEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 3,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            ContractId = SampleData.FuturesOptionContracts[0].ContractId,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

    static FuturesOptionTickBidAskEvent CreateTickPriceDataEvent(Guid? commandId = null)
    {
        var entityId = SampleData.OptionTickStreamingFeedId;
        var tickPriceData = new FuturesOptionTickBidAskReadModel(
            DateTime.UtcNow, DateTime.UtcNow.Ticks, 12.25, 12.75, 100, 150);
        return new FuturesOptionTickBidAskEvent(1001, tickPriceData)
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataEventActor.Actor, FuturesOptionTickBidAskEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 4,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test"
        };
    }

}
