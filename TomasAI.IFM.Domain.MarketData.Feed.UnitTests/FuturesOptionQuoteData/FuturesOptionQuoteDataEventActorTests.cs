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
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesOptionQuoteData;

public class FuturesOptionQuoteDataEventActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesOptionQuoteDataEventActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesOptionQuoteDataEventActor : FuturesOptionQuoteDataEventActor
    {
        public TestableFuturesOptionQuoteDataEventActor(IActorSupervisor supervisor,
             IMarketDataSnapshotApi marketDataSnapshotApi,
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesOptionQuoteDataEventActor> logger)
            : base(supervisor, marketDataSnapshotApi, blackboardService, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    static FuturesOptionQuoteDataInsertedCompleteEvent CreateInsertedCompleteEvent(Guid? commandId = null)
    {
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        return new FuturesOptionQuoteDataInsertedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, FuturesOptionQuoteDataInsertedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            QuoteId = SampleData.OptionQuoteStreamId,
            OptionQuoteData = new FuturesOptionQuoteDataReadModel(SampleData.OptionQuoteStreamId, "ES20240601C5500", 1001, 12.50m, 10, 13.00m, 5),
            UpdatedOn = DateTime.UtcNow,
            UpdatedBy = "UnitTest"
        };
    }

    static FuturesOptionQuoteDataStreamingStartedCompleteEvent CreateStreamingStartedCompleteEvent(Guid? commandId = null)
    {
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        return new FuturesOptionQuoteDataStreamingStartedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, FuturesOptionQuoteDataStreamingStartedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            QuoteId = SampleData.OptionQuoteStreamId,
            FuturesOptionQuotes = SampleData.FuturesOptionQuotes,
            FuturesOptionContracts = SampleData.FuturesOptionContracts,
            FuturesOptionQuoteData = [.. SampleData.FuturesOptionQuotes.Select(o => new FuturesOptionQuoteDataReadModel(o.QuoteId, o.ContractId, o.RequestId, -1, -1, -1, -1))],
            StartedOn = DateTime.UtcNow,
            StartedBy = "UnitTest"
        };
    }

    static FuturesOptionQuoteDataStreamingStoppedCompleteEvent CreateStreamingStoppedCompleteEvent(Guid? commandId = null)
    {
        var entityId = new QuoteId(SampleData.OptionQuoteStreamId);
        return new FuturesOptionQuoteDataStreamingStoppedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataEventActor.Actor, FuturesOptionQuoteDataStreamingStoppedCompleteEvent.Verb, entityId.Format()),
            Id = Guid.NewGuid(),
            CommandId = commandId ?? Guid.NewGuid(),
            EntityId = entityId,
            EventId = 2,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            QuoteId = SampleData.OptionQuoteStreamId,
            FuturesOptionQuotes = SampleData.FuturesOptionQuotes,
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "UnitTest"
        };
    }

}
