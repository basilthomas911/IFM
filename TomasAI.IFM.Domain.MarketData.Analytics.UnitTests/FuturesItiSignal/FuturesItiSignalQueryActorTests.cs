using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public class FuturesItiSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesItiSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesItiSignalQueryActor : FuturesItiSignalQueryActor
    {
        public TestableFuturesItiSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesItiSignalQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query)
            => await OnLoadStateAsync(context, threadId, query);
    }

    #region Helpers

    private class DummyQueryState : IActorState
    {
        public ActorThreadId Id { get; set; }
    }

    private static GetFuturesItiSignalQuery CreateGetSignalQuery()
    {
        var entityId = SampleData.EntityId;
        return new GetFuturesItiSignalQuery(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalQuery.Actor, GetFuturesItiSignalQuery.Verb, entityId.Format())
        };
    }

    private static GetFuturesItiSignalDataQuery CreateGetSignalDataQuery()
    {
        var entityId = SampleData.EntityId;
        return new GetFuturesItiSignalDataQuery(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalDataQuery.Actor, GetFuturesItiSignalDataQuery.Verb, entityId.Format())
        };
    }

    private static GetFuturesItiTrendDirectionChangedSignalsQuery CreateGetTrendDirectionChangedQuery()
    {
        var entityId = SampleData.EntityId;
        return new GetFuturesItiTrendDirectionChangedSignalsQuery(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiTrendDirectionChangedSignalsQuery.Actor, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, entityId.Format())
        };
    }

    private static FuturesItiSignalV2ReadModel CreateReadModel()
        => SampleData.StartOfDayEvent.FuturesItiSignal!;

    public static IEnumerable<object[]> SupportedTimePeriods =>
    [
        [TradeTimePeriodType.Daily],
        [TradeTimePeriodType.Weekly],
        [TradeTimePeriodType.Monthly]
    ];

    #endregion

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_DeserializesGetFuturesItiSignalQuery_Successfully()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesItiSignalQuery>();
        var deserialized = result as GetFuturesItiSignalQuery;
        deserialized.Should().NotBeNull();
        deserialized!.ContractId.Should().Be(query.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);
    }

    [Fact]
    public void ParseMessage_DeserializesGetFuturesItiSignalDataQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalDataQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesItiSignalDataQuery>();
        var deserialized = result as GetFuturesItiSignalDataQuery;
        deserialized.Should().NotBeNull();
        deserialized!.ContractId.Should().Be(query.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);
    }

    [Fact]
    public void ParseMessage_DeserializesGetFuturesItiTrendDirectionChangedSignalsQuery_Successfully()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetTrendDirectionChangedQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesItiTrendDirectionChangedSignalsQuery>();
        var deserialized = result as GetFuturesItiTrendDirectionChangedSignalsQuery;
        deserialized.Should().NotBeNull();
        deserialized!.ContractId.Should().Be(query.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);
    }

    [Fact]
    public void ParseMessage_CallsSetMessageInfo_ExactlyOnce()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetFuturesItiSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ExtractsThreadIdFromSubject_Correctly()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetFuturesItiSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void ParseMessage_DeserializesGetFuturesItiSignalQuery_AcrossTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var entityId = SampleData.EntityIdFor(timePeriod);
        var query = new GetFuturesItiSignalQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalQuery.Actor, GetFuturesItiSignalQuery.Verb, entityId.Format())
        };

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().BeOfType<GetFuturesItiSignalQuery>();
        (result as GetFuturesItiSignalQuery)!.TimePeriod.Should().Be(timePeriod);
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        // Act
        Action act = () => actor.InvokeParseMessage(null!, natsMsg);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenActorNameDoesNotMatch()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = new ActorSubject(ActorType.Query, "SomeOtherActor", GetFuturesItiSignalQuery.Verb, SampleData.EntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesItiSignalQueryActor.ActorName} query from message:*");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRegistered()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = new ActorSubject(ActorType.Query, FuturesItiSignalQueryActor.ActorName, "UnknownVerb", SampleData.EntityId.Format()).ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsInvalid()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var subject = query.Subject.ToString();
        var invalidPayload = new byte[] { 0xFF, 0xFE, 0xFD };
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, invalidPayload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsEmpty()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var subject = query.Subject.ToString();
        var emptyPayload = Array.Empty<byte>();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, emptyPayload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<Exception>();
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_GetFuturesItiSignalQuery_ExecutesHandler_AndReplies()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var readModel = CreateReadModel();
        marketDataDb.GetLastFuturesItiSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(readModel));

        var actor = _fixture.CreateItiQueryActor(dbFactory);
        var query = CreateGetSignalQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesItiSignalQuery.Verb, Arg.Any<ServiceResult<FuturesItiSignalV2ReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await marketDataDb.Received(1).GetLastFuturesItiSignalAsync(query.ContractId, query.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesItiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel?>>(r => r.Success && r.Value == readModel));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesItiSignalQuery_ReturnsNullResult_WhenNoSignalFound()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        marketDataDb.GetLastFuturesItiSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(null));

        var actor = _fixture.CreateItiQueryActor(dbFactory);
        var query = CreateGetSignalQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesItiSignalQuery.Verb, Arg.Any<ServiceResult<FuturesItiSignalV2ReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesItiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel?>>(r => r.Success && r.Value == null));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesItiSignalDataQuery_ExecutesHandler_AndReplies()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var readModel = CreateReadModel();
        marketDataDb.GetLastFuturesItiSignalTrendDirectionChangeAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(readModel));
        marketDataDb.GetLastFuturesItiSignalTrendExtremeChangeAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(readModel));
        marketDataDb.GetLastFuturesItiSignalTrendReversalChangeAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(readModel));

        var actor = _fixture.CreateItiQueryActor(dbFactory);
        var query = CreateGetSignalDataQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesItiSignalDataQuery.Verb, Arg.Any<ServiceResult<FuturesItiSignalDataReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await marketDataDb.Received(1).GetLastFuturesItiSignalTrendDirectionChangeAsync(query.ContractId, query.ValueDate);
        await marketDataDb.Received(1).GetLastFuturesItiSignalTrendExtremeChangeAsync(query.ContractId, query.ValueDate);
        await marketDataDb.Received(1).GetLastFuturesItiSignalTrendReversalChangeAsync(query.ContractId, query.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesItiSignalDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesItiSignalDataReadModel?>>(r => r.Success && r.Value != null));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesItiTrendDirectionChangedSignalsQuery_ExecutesHandler_AndReplies()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var readModel = CreateReadModel();
        marketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<ICollection<FuturesItiSignalV2ReadModel>>([readModel]));

        var actor = _fixture.CreateItiQueryActor(dbFactory);
        var query = CreateGetTrendDirectionChangedQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, Arg.Any<ServiceResult<FuturesItiSignalV2ReadModel[]>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await marketDataDb.Received(1).GetFuturesItiTrendDirectionChangedSignalsAsync(query.ContractId, query.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesItiTrendDirectionChangedSignalsQuery.Verb,
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel[]>>(r => r.Success && r.Value!.Length == 1 && r.Value[0] == readModel));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesItiTrendDirectionChangedSignalsQuery_ReturnsEmptyArray_WhenNoneFound()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        marketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<ICollection<FuturesItiSignalV2ReadModel>>([]));

        var actor = _fixture.CreateItiQueryActor(dbFactory);
        var query = CreateGetTrendDirectionChangedQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, Arg.Any<ServiceResult<FuturesItiSignalV2ReadModel[]>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesItiTrendDirectionChangedSignalsQuery.Verb,
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel[]>>(r => r.Success && r.Value!.Length == 0));
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public async Task ReceiveAsync_GetFuturesItiSignalQuery_ExecutesHandler_AcrossTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var readModel = CreateReadModel();
        marketDataDb.GetLastFuturesItiSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalV2ReadModel?>(readModel));

        var actor = _fixture.CreateItiQueryActor(dbFactory);
        var entityId = SampleData.EntityIdFor(timePeriod);
        var query = new GetFuturesItiSignalQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalQuery.Actor, GetFuturesItiSignalQuery.Verb, entityId.Format())
        };
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesItiSignalQuery.Verb, Arg.Any<ServiceResult<FuturesItiSignalV2ReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await marketDataDb.Received(1).GetLastFuturesItiSignalAsync(query.ContractId, query.ValueDate);
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, state, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, null!, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var state = new DummyQueryState { Id = new ActorThreadId(ActorType.Query, FuturesItiSignalQueryActor.ActorName, "test-thread") };
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenQueryTypeIsNotRecognized()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var state = new DummyQueryState { Id = new ActorThreadId(ActorType.Query, FuturesItiSignalQueryActor.ActorName, "test-thread") };
        var context = Substitute.For<IQueryActorContext>();
        var unsupportedQuery = Substitute.For<IQuery>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, state, unsupportedQuery);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Unable to process");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_GetFuturesItiSignalQuery_RepliesWithServiceResultContainingErrorDetails()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        ServiceResult<FuturesItiSignalV2ReadModel?>? capturedResult = null;
        context.ReplyAsync(threadId, GetFuturesItiSignalQuery.Verb, Arg.Do<ServiceResult<FuturesItiSignalV2ReadModel?>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesItiSignalQuery.Verb, exception);

        // Assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Success.Should().BeFalse();
        capturedResult.ErrorCode.Should().Be(query.ErrorCode);
        capturedResult.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task OnExceptionAsync_GetFuturesItiSignalDataQuery_RepliesWithServiceResultContainingErrorDetails()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalDataQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        ServiceResult<FuturesItiSignalDataReadModel?>? capturedResult = null;
        context.ReplyAsync(threadId, GetFuturesItiSignalDataQuery.Verb, Arg.Do<ServiceResult<FuturesItiSignalDataReadModel?>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesItiSignalDataQuery.Verb, exception);

        // Assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Success.Should().BeFalse();
        capturedResult.ErrorCode.Should().Be(query.ErrorCode);
        capturedResult.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task OnExceptionAsync_GetFuturesItiTrendDirectionChangedSignalsQuery_RepliesWithServiceResultContainingErrorDetails()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetTrendDirectionChangedQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        ServiceResult<FuturesItiSignalV2ReadModel[]>? capturedResult = null;
        context.ReplyAsync(threadId, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, Arg.Do<ServiceResult<FuturesItiSignalV2ReadModel[]>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, exception);

        // Assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Success.Should().BeFalse();
        capturedResult.ErrorCode.Should().Be(query.ErrorCode);
        capturedResult.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task OnExceptionAsync_UnsupportedQueryType_RepliesWithGenericServiceFailed()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var threadId = new ActorThreadId(ActorType.Query, FuturesItiSignalQueryActor.ActorName, "test-thread");
        var unsupportedQuery = Substitute.For<IQuery>();
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();
        var verb = "UnknownVerb";

        ServiceFailed<ActorEntityId>? capturedResult = null;
        context.ReplyAsync(threadId, verb, Arg.Do<ServiceFailed<ActorEntityId>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, unsupportedQuery, verb, exception);

        // Assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Success.Should().BeFalse();
        capturedResult.ErrorCode.Should().Be(9999);
        capturedResult.ErrorMessage.Should().Be(exception.Message);
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetFuturesItiSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var context = Substitute.For<IQueryActorContext>();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, default, query, GetFuturesItiSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var threadId = new ActorThreadId(ActorType.Query, FuturesItiSignalQueryActor.ActorName, "test-thread");
        var context = Substitute.For<IQueryActorContext>();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetFuturesItiSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var threadId = query.Subject.ThreadId;
        var context = Substitute.For<IQueryActorContext>();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, null!, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenExceptionIsNull()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var threadId = query.Subject.ThreadId;
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesItiSignalQuery.Verb, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_DoesNotThrow_WhenReplyAsyncThrows()
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var query = CreateGetSignalQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(threadId, GetFuturesItiSignalQuery.Verb, Arg.Any<ServiceResult<FuturesItiSignalV2ReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Reply failed"));

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesItiSignalQuery.Verb, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public async Task OnExceptionAsync_GetFuturesItiSignalQuery_RepliesWithErrorDetails_AcrossTimePeriods(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateItiQueryActor();
        var entityId = SampleData.EntityIdFor(timePeriod);
        var query = new GetFuturesItiSignalQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalQuery.Actor, GetFuturesItiSignalQuery.Verb, entityId.Format())
        };
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        ServiceResult<FuturesItiSignalV2ReadModel?>? capturedResult = null;
        context.ReplyAsync(threadId, GetFuturesItiSignalQuery.Verb, Arg.Do<ServiceResult<FuturesItiSignalV2ReadModel?>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesItiSignalQuery.Verb, exception);

        // Assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Success.Should().BeFalse();
        capturedResult.ErrorMessage.Should().Be(exception.Message);
    }

    #endregion
}