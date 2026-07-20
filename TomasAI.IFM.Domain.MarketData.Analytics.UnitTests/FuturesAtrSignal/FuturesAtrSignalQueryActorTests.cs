using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal;

public class FuturesAtrSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesAtrSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesAtrSignalQueryActor : FuturesAtrSignalQueryActor
    {
        public TestableFuturesAtrSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesAtrSignalQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);
    }

    #region Helper

    private class DummyQueryState : IActorState
    {
        public ActorThreadId Id { get; set; }
    }

    private static GetFuturesAtrSignalQuery CreateQuery()
    {
        var entityId = SampleData.AtrEntityId;
        return new GetFuturesAtrSignalQuery(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesAtrSignalQuery.Actor, GetFuturesAtrSignalQuery.Verb, entityId.Format())
        };
    }

    private static GetFuturesAtrDailySignalQuery CreateDailyQuery()
    {
        var entityId = SampleData.AtrEntityId;
        return new GetFuturesAtrDailySignalQuery(SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesAtrDailySignalQuery.Actor, GetFuturesAtrDailySignalQuery.Verb, entityId.Format())
        };
    }

    private static FuturesAtrSignalReadModel CreateAtrSignalReadModel()
        => SampleData.CreateAtrSignalGeneratedEvent().FuturesAtrSignal!;

    #endregion

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_DeserializesGetFuturesAtrSignalQuery_Successfully()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesAtrSignalQuery>();
        var deserialized = result as GetFuturesAtrSignalQuery;
        deserialized.Should().NotBeNull();
        deserialized!.ContractId.Should().Be(query.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);
    }

    [Fact]
    public void ParseMessage_CallsSetMessageInfo_ExactlyOnce()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetFuturesAtrSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ExtractsThreadIdFromSubject_Correctly()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetFuturesAtrSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_DeserializesGetFuturesAtrDailySignalQuery_Successfully()
    {
        // Arrange - the production _parseMap registers both GetFuturesAtrSignalQuery.Verb and
        // GetFuturesAtrDailySignalQuery.Verb, so the daily query verb is resolvable via ParseMessage.
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateDailyQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesAtrDailySignalQuery>();
        var deserialized = result as GetFuturesAtrDailySignalQuery;
        deserialized.Should().NotBeNull();
        deserialized!.ContractId.Should().Be(query.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
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
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var invalidSubject = $"Query.WrongActorName.{GetFuturesAtrSignalQuery.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAtrSignalQueryActor.ActorName} query from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var invalidSubject = $"Query.{FuturesAtrSignalQueryActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAtrSignalQueryActor.ActorName} query from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsCorrupted()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var corruptedPayload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, corruptedPayload, default!, NatsMsgFlags.None);

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
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var emptyPayload = Array.Empty<byte>();
        var subject = query.Subject.ToString();
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
    public async Task ReceiveAsync_GetFuturesAtrSignalQuery_ExecutesHandler_AndReplies()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var expectedReadModel = CreateAtrSignalReadModel();
        marketDataDb.GetLastFuturesAtrSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TradeTimePeriodType>(), Arg.Any<int>())
            .Returns(Task.FromResult<FuturesAtrSignalReadModel?>(expectedReadModel));

        var actor = _fixture.CreateAtrQueryActor(dbFactory: dbFactory);

        var query = CreateQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesAtrSignalQuery.Verb, Arg.Any<ServiceResult<FuturesAtrSignalReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await marketDataDb.Received(1).GetLastFuturesAtrSignalAsync(query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(r => r.Success && r.Value == expectedReadModel));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesAtrDailySignalQuery_ExecutesHandler_AndReplies()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var expectedReadModel = CreateAtrSignalReadModel();
        marketDataDb.GetLastFuturesAtrDailySignalAsync(Arg.Any<string>(), Arg.Any<TradeTimePeriodType>(), Arg.Any<int>())
            .Returns(Task.FromResult<FuturesAtrSignalReadModel?>(expectedReadModel));

        var actor = _fixture.CreateAtrQueryActor(dbFactory: dbFactory);

        var query = CreateDailyQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesAtrDailySignalQuery.Verb, Arg.Any<ServiceResult<FuturesAtrSignalReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await marketDataDb.Received(1).GetLastFuturesAtrDailySignalAsync(query.ContractId, query.TimePeriod, query.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrDailySignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(r => r.Success && r.Value == expectedReadModel));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesAtrSignalQuery_ReturnsNullReadModel_WhenNoDataFound()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        marketDataDb.GetLastFuturesAtrSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TradeTimePeriodType>(), Arg.Any<int>())
            .Returns(Task.FromResult<FuturesAtrSignalReadModel?>(null));

        var actor = _fixture.CreateAtrQueryActor(dbFactory: dbFactory);

        var query = CreateQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesAtrSignalQuery.Verb, Arg.Any<ServiceResult<FuturesAtrSignalReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, state, query);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(r => r.Success && r.Value == null));
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, state, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var state = new DummyQueryState { Id = new ActorThreadId(ActorType.Query, FuturesAtrSignalQueryActor.ActorName, "test-thread") };
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
        var actor = _fixture.CreateAtrQueryActor();

        var state = new DummyQueryState { Id = new ActorThreadId(ActorType.Query, FuturesAtrSignalQueryActor.ActorName, "test-thread") };
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
    public async Task OnExceptionAsync_GetFuturesAtrSignalQuery_RepliesWithServiceResultContainingErrorDetails()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        ServiceResult<FuturesAtrSignalReadModel?>? capturedResult = null;
        context.ReplyAsync(threadId, GetFuturesAtrSignalQuery.Verb, Arg.Do<ServiceResult<FuturesAtrSignalReadModel?>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesAtrSignalQuery.Verb, exception);

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
        var actor = _fixture.CreateAtrQueryActor();

        var threadId = new ActorThreadId(ActorType.Query, FuturesAtrSignalQueryActor.ActorName, "test-thread");
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

    [Fact]
    public async Task OnExceptionAsync_GetFuturesAtrDailySignalQuery_RepliesWithGenericServiceFailed()
    {
        // Arrange - the production OnExceptionAsync only special-cases GetFuturesAtrSignalQuery,
        // so the daily-signal query falls through to the generic ServiceFailed<ActorEntityId> reply.
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateDailyQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        ServiceFailed<ActorEntityId>? capturedResult = null;
        context.ReplyAsync(threadId, GetFuturesAtrDailySignalQuery.Verb, Arg.Do<ServiceFailed<ActorEntityId>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesAtrDailySignalQuery.Verb, exception);

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
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetFuturesAtrSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var context = Substitute.For<IQueryActorContext>();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, default, query, GetFuturesAtrSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var threadId = new ActorThreadId(ActorType.Query, FuturesAtrSignalQueryActor.ActorName, "test-thread");
        var context = Substitute.For<IQueryActorContext>();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetFuturesAtrSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
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
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesAtrSignalQuery.Verb, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_SwallowsInnerException_WhenReplyAsyncThrows()
    {
        // Arrange
        var actor = _fixture.CreateAtrQueryActor();

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(threadId, GetFuturesAtrSignalQuery.Verb, Arg.Any<ServiceResult<FuturesAtrSignalReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Reply failed"));

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesAtrSignalQuery.Verb, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}
