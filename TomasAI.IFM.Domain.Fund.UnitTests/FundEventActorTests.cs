using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Event.Actor;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public class FundEventActorTests : IClassFixture<FundTestFixture>
{
    readonly FundTestFixture _fixture;

    public FundEventActorTests(FundTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ParseMessage_ShouldDeserializeSupportedEvents_AndSetProperties()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        var fundOrder = SampleData.FundOrder;
        var maxProfit = new FundMaxProfitReadModel(fundOrder.Id, 1000m, 0.05);
        var @event = new FundMaxProfitGeneratedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundEventActor.Actor, FundMaxProfitGeneratedEvent.Verb, "123"),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = new FundId(SampleData.FundOrder.FundId),
            EventId = 1,
            ReceivedOn = DateTime.UtcNow,
            EventSource = "test",
            FundOrder = fundOrder,
            FundMaxProfit = maxProfit
        };

        var subject = $"Event.{FundEventActor.Actor}.{FundMaxProfitGeneratedEvent.Verb}.{@event.EntityId}";
        var serializedData = _fixture.DataSerializer.Serialize(@event);
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = serializedData
        };

        var result = actor.InvokeParseMessage(mockContext, message);

        result.Should().NotBeNull();
        result.Should().BeOfType<FundMaxProfitGeneratedEvent>();
        var parsedEvent = (FundMaxProfitGeneratedEvent)result;
        parsedEvent.CommandId.Should().Be(@event.CommandId);
        parsedEvent.Id.Should().Be(@event.Id);
        parsedEvent.FundMaxProfit.Should().BeEquivalentTo(@event.FundMaxProfit);
    }

    [Fact]
    public void ParseMessage_ShouldReturnNull_WhenActorTypeOrNameMismatch()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        var invalidSubject = $"Event.WrongActor.{FundMaxProfitGeneratedEvent.Verb}.123";
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject,
            Data = _fixture.DataSerializer.Serialize(SampleData.FundMaxProfitGeneratedEvent)
        };

        var result = actor.InvokeParseMessage(mockContext, message);
        result.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_WithNullContext_ShouldThrowArgumentNullException()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var message = new NatsMsg<byte[]>
        {
            Subject = $"Event.{FundEventActor.Actor}.{FundMaxProfitGeneratedEvent.Verb}.123",
            Data = _fixture.DataSerializer.Serialize(SampleData.FundMaxProfitGeneratedEvent)
        };

        Action act = () => actor.InvokeParseMessage(null!, message);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_WithUnknownVerb_ShouldReturnNull()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        var subject = $"Event.{FundEventActor.Actor}.UnknownVerb.123";
        var message = new NatsMsg<byte[]>
        {
            Subject = subject,
            Data = Array.Empty<byte>()
        };

        var result = actor.InvokeParseMessage(mockContext, message);
        result.Should().BeNull();
    }

    // Test helper to expose protected ParseMessage and ReceiveAsync for unit testing.
    public class TestableFundEventActor : FundEventActor
    {
        public TestableFundEventActor(IActorSupervisor supervisor, ILogger<FundEventActor> logger)
            : base(supervisor, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);
    }

    #region ReceiveAsync Tests

    static FundMaxProfitGeneratedReadModel CreateMaxProfitData(int fundId) => new(
        fundId: fundId,
        tradeDate: DateOnly.FromDateTime(DateTime.UtcNow),
        fundBalance: 1000m,
        fundProfitOrders: new List<FundOrderAmountReadModel>(),
        fundLossOrders: new List<FundOrderAmountReadModel>(),
        fundDrawdownBalances: new FundDrawdownBalancesReadModel(fundId, 900m, 1000m));

    static FuturesTradeSignalV2ReadModel CreateTradeSignal() => new(
        contractId: "ESU9",
        valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
        timePeriod: TradeTimePeriodType.Daily,
        sequenceId: 1,
        timestamp: TimeOnly.FromDateTime(DateTime.UtcNow),
        mean: 0, stdDev: 0, futuresPrice: 0, priceChangePercent: 0,
        fundRiskPercent: 0.1,
        rsi: 0, rsiSlope: 0,
        trendType: FuturesTrendType.RangeBound,
        trendStrength: FuturesTrendStrengthType.None,
        tradeSignal: TradeSignalType.None,
        tdi: FuturesTrendDirectionType.Flat,
        tdiStrength: FuturesTrendDirectionStrengthType.Low,
        mdi: 0,
        mdiTrend: FuturesMDITrendType.RangeBound,
        mdiUpTrendLimit: 0, mdiDownTrendLimit: 0,
        upTrendingTrigger: 0, downTrendingTrigger: 0,
        entryTrigger: 0, exitTrigger: 0,
        trendDelta: 0, trendExtreme: 0, trendReversal: 0,
        fiftyDMA: 0, twoHundredDMA: 0,
        tradeExecuteState: TradeExecuteState.No);

    [Fact]
    public async Task ReceiveAsync_FundMaxProfitGeneratedEvent_HappyPath_CompletesSuccessfully()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        var maxProfitData = CreateMaxProfitData(SampleData.FundOrder.FundId);
        mockContext.RequestAsync<FundMaxProfitGeneratedReadModel, GetFundMaxProfitGeneratedQuery>(Arg.Any<GetFundMaxProfitGeneratedQuery>())
            .Returns(new ServiceResult<FundMaxProfitGeneratedReadModel>(maxProfitData));
        var tradeSignal = CreateTradeSignal();
        mockContext.RequestAsync<FuturesTradeSignalV2ReadModel, GetFuturesTradeSignalQuery>(Arg.Any<GetFuturesTradeSignalQuery>())
            .Returns(new ServiceResult<FuturesTradeSignalV2ReadModel>(tradeSignal));
        mockContext.SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(Arg.Any<FundMaxProfitGeneratedCompleteEvent>())
            .Returns(ValueTask.CompletedTask);

        var @event = SampleData.FundMaxProfitGeneratedEvent;

        await actor.InvokeReceiveAsync(mockContext, @event);

        await mockContext.Received(1).SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(
            Arg.Is<FundMaxProfitGeneratedCompleteEvent>(e => e.FundMaxProfit != null));
    }

    [Fact]
    public async Task ReceiveAsync_FundMaxProfitGeneratedEvent_WhenQueryFails_SendsFailEvent()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        mockContext.RequestAsync<FundMaxProfitGeneratedReadModel, GetFundMaxProfitGeneratedQuery>(Arg.Any<GetFundMaxProfitGeneratedQuery>())
            .Returns(new ServiceResult<FundMaxProfitGeneratedReadModel>(9999, "boom"));
        mockContext.SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(Arg.Any<FundMaxProfitGeneratedFailEvent>())
            .Returns(ValueTask.CompletedTask);

        var @event = SampleData.FundMaxProfitGeneratedEvent;

        await actor.InvokeReceiveAsync(mockContext, @event);

        await mockContext.Received(1).SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(Arg.Any<FundMaxProfitGeneratedFailEvent>());
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var @event = SampleData.FundMaxProfitGeneratedEvent;

        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, @event);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenEventIsNull()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);
        var mockContext = Substitute.For<IEventActorContext>();

        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenEventUnsupported()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);
        var mockContext = Substitute.For<IEventActorContext>();

        var unknownEvent = Substitute.For<IEvent>();
        unknownEvent.Subject.Returns(new ActorSubject(ActorType.Event, FundEventActor.Actor, "UnknownVerb", "123"));

        Func<Task> act = async () => await actor.InvokeReceiveAsync(mockContext, unknownEvent);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FundEventActor.Actor} event from message:*");
    }

    #endregion

    #region OnExceptionAsync Tests

    [Fact]
    public async Task OnExceptionAsync_WithValidException_SendsEventExceptionEvent()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FundEventActor.Actor, "1");
        var @event = SampleData.FundMaxProfitGeneratedEvent;
        var exception = new InvalidOperationException("Test exception message");

        mockContext.SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        await mockContext.Received(1).SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage == exception.Message &&
                e.ErrorType == ErrorType.EventService));
    }

    [Fact]
    public async Task OnExceptionAsync_WithArgumentNullException_SendsEventExceptionEvent()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FundEventActor.Actor, "1");
        var @event = SampleData.FundMaxProfitGeneratedEvent;
        var exception = new ArgumentNullException("paramName", "Parameter cannot be null");

        mockContext.SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(ValueTask.CompletedTask);

        await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);

        await mockContext.Received(1).SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Is<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent>(e =>
                e.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task OnExceptionAsync_WhenSendAsyncThrows_FallsBackAndSendsAgain()
    {
        var mockSupervisor = Substitute.For<IActorSupervisor>();
        var mockLogger = Substitute.For<ILogger<FundEventActor>>();
        var actor = _fixture.CreateActor(mockSupervisor, mockLogger);

        var mockContext = Substitute.For<IEventActorContext>();
        var threadId = new ActorThreadId(ActorType.Event, FundEventActor.Actor, "1");
        var @event = SampleData.FundMaxProfitGeneratedEvent;
        var exception = new InvalidOperationException("Test exception message");

        var callCount = 0;
        mockContext.SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("send failed");
                return ValueTask.CompletedTask;
            });

        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(mockContext, threadId, @event, exception);
        await act.Should().NotThrowAsync();

        // the internal SendErrorEventAsync retries once with a fallback error event after the first send fails
        await mockContext.Received(2).SendAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            Arg.Any<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent>());
    }

    #endregion
}
