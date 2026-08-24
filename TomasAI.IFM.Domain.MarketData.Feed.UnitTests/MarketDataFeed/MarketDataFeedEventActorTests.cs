using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Event;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

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
        public IMarketDataFeedEventContext Context { get; }

        public TestableMarketDataFeedEventActor(
            IActorSupervisor supervisor,
            ApplicationMarketDataApi marketDataApi,
            IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
            IBlackboardService blackboardService,
             IStatusConsoleWriter statusConsoleWriter,
            ILogger<MarketDataFeedEventActor> logger)
            : this(TypedActorContextFactory.Event(
                supervisor, marketDataApi, optionTradeLiveFeedMap,
                blackboardService, statusConsoleWriter, logger)) { }

        TestableMarketDataFeedEventActor(IMarketDataFeedEventContext context)
            : base(context) => Context = context;

        public IEvent InvokeParseMessage(IEventActorContext<MarketDataFeedEventActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext<MarketDataFeedEventActor> context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext<MarketDataFeedEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    [Fact]
    public async Task StartedComplete_ActivatesEveryFuturesTickStream_AndTheBarStream()
    {
        var actor = _fixture.CreateMarketDataFeedEventActor();
        IEventActorContext<MarketDataFeedEventActor> context = actor.Context;
        context.RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(
                Arg.Any<StartFuturesTickDataStreamingCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        context.RequestAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(
                Arg.Any<StartFuturesBarDataStreamingCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        var @event = CreateStartedCompleteEvent();

        await actor.InvokeReceiveAsync(context, @event);

        await context.Received(SampleData.FuturesContracts.Length)
            .RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(
                Arg.Any<StartFuturesTickDataStreamingCommand>());
        foreach (var contract in SampleData.FuturesContracts)
        {
            await context.Received(1)
                .RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(
                    Arg.Is<StartFuturesTickDataStreamingCommand>(command =>
                        command.Contract.ContractId == contract.ContractId
                        && command.EntityId.ContractId == contract.ContractId
                        && command.EntityId.ValueDate == @event.ValueDate));
        }
        await context.Received(1)
            .RequestAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(
                Arg.Is<StartFuturesBarDataStreamingCommand>(command =>
                    command.ValueDate == @event.ValueDate
                    && command.Contracts.Select(value => value.ContractId)
                        .SequenceEqual(SampleData.FuturesContracts.Select(value => value.ContractId))));
    }

    [Fact]
    public async Task StartedComplete_DoesNotReportOrContinueAfterTickStreamCommandIsRejected()
    {
        var status = Substitute.For<IStatusConsoleWriter>();
        var actor = _fixture.CreateMarketDataFeedEventActor(statusConsoleWriter: status);
        IEventActorContext<MarketDataFeedEventActor> context = actor.Context;
        context.RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(
                Arg.Any<StartFuturesTickDataStreamingCommand>())
            .Returns(new ServiceFailed<GuidResult>(6003, "route rejected"));
        var @event = CreateStartedCompleteEvent();
        var firstContract = SampleData.FuturesContracts[0];

        await actor.InvokeReceiveAsync(context, @event);

        await context.Received(1)
            .RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(
                Arg.Any<StartFuturesTickDataStreamingCommand>());
        await context.DidNotReceive()
            .RequestAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(
                Arg.Any<StartFuturesBarDataStreamingCommand>());
        await status.DidNotReceive().WriteConsoleAsync(
            LogSourceType.MarketDataFeedEvent,
            $"Streaming Futures {firstContract.ContractId} started");
        await status.Received(1).WriteConsoleAsync(
            LogSourceType.MarketDataFeedEvent,
            -1,
            Arg.Is<string>(message => message.Contains("route rejected", StringComparison.Ordinal)),
            string.Empty,
            string.Empty);
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
