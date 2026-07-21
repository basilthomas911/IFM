using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAdxSignal;

public class FuturesAdxSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesAdxSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesAdxSignalQueryActor : FuturesAdxSignalQueryActor
    {
        public TestableFuturesAdxSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesAdxSignalQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);
    }

    #region Helper

    private class DummyQueryState : IActorState
    {
        public ActorThreadId Id { get; set; }
    }

    private static GetFuturesAdxSignalQuery CreateQuery()
    {
        var entityId = SampleData.AdxEntityId;
        return new GetFuturesAdxSignalQuery(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesAdxSignalQuery.Actor, GetFuturesAdxSignalQuery.Verb, entityId.Format())
        };
    }

    private static GetFuturesAdxDailySignalQuery CreateDailyQuery()
    {
        var entityId = SampleData.AdxEntityId;
        return new GetFuturesAdxDailySignalQuery(SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength) with
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesAdxDailySignalQuery.Actor, GetFuturesAdxDailySignalQuery.Verb, entityId.Format())
        };
    }

    #endregion

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_DeserializesGetFuturesAdxSignalQuery_Successfully()
    {
        // Arrange
        _fixture.DataSerializer.Should().NotBeNull();
        _fixture.MsgSerializer.Should().NotBeNull();

        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        var result = actor.InvokeParseMessage(context, natsMsg);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesAdxSignalQuery>();
        var deserialized = result as GetFuturesAdxSignalQuery;
        deserialized.Should().NotBeNull();
        deserialized!.ContractId.Should().Be(query.ContractId);
        deserialized.Subject.ToString().Should().Be(subject);
    }

    [Fact]
    public void ParseMessage_CallsSetMessageInfo_ExactlyOnce()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

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
            Arg.Is(GetFuturesAdxSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ExtractsThreadIdFromSubject_Correctly()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

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
            Arg.Is(GetFuturesAdxSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_ForGetFuturesAdxDailySignalQuery_VerbNotRegistered()
    {
        // Arrange - the production _parseMap only registers GetFuturesAdxSignalQuery.Verb, so the
        // daily query verb is not resolvable via ParseMessage even though ReceiveAsync supports it.
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateDailyQuery();

        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var subject = query.Subject.ToString();
        var natsMsg = new NatsMsg<byte[]>(subject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAdxSignalQueryActor.ActorName} query from message: {subject}");
    }

    [Fact]
    public void ParseMessage_DoesNotModifyOriginalMessage()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var originalSubject = query.Subject.ToString();
        var originalDataLength = payload.Length;
        var natsMsg = new NatsMsg<byte[]>(originalSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        actor.InvokeParseMessage(context, natsMsg);

        // Assert
        natsMsg.Subject.Should().Be(originalSubject);
        natsMsg.Data!.Length.Should().Be(originalDataLength);
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

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
        var actor = _fixture.CreateAdxQueryActor();

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var invalidSubject = $"Query.WrongActorName.{GetFuturesAdxSignalQuery.Verb}.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAdxSignalQueryActor.ActorName} query from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsInvalidOperationException_WhenVerbIsNotRecognized()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

        var query = CreateQuery();
        var payload = ActorExtensions.DataSerializer.Serialize(query);
        var invalidSubject = $"Query.{FuturesAdxSignalQueryActor.ActorName}.UnknownVerb.{Guid.NewGuid()}";
        var natsMsg = new NatsMsg<byte[]>(invalidSubject, string.Empty, 0, default!, payload, default!, NatsMsgFlags.None);

        var context = Substitute.For<IQueryActorContext>();

        // Act
        Action act = () => actor.InvokeParseMessage(context, natsMsg);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAdxSignalQueryActor.ActorName} query from message: {invalidSubject}");
    }

    [Fact]
    public void ParseMessage_ThrowsException_WhenPayloadIsCorrupted()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

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
        var actor = _fixture.CreateAdxQueryActor();

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
    public async Task ReceiveAsync_GetFuturesAdxSignalQuery_ExecutesHandler_AndReplies()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        marketDataDb.GetLastFuturesAdxSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TradeTimePeriodType>(), Arg.Any<int>())
            .Returns(Task.FromResult<FuturesAdxSignalReadModel?>(SampleData.FuturesAdxSignal));

        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesAdxSignalQuery.Verb, Arg.Any<ServiceResult<FuturesAdxSignalReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await marketDataDb.Received(1).GetLastFuturesAdxSignalAsync(query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAdxSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAdxSignalReadModel?>>(r => r.Success && r.Value == SampleData.FuturesAdxSignal));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesAdxDailySignalQuery_ExecutesHandler_AndReplies()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        marketDataDb.GetLastFuturesAdxDailySignalAsync(Arg.Any<string>(), Arg.Any<TradeTimePeriodType>(), Arg.Any<int>())
            .Returns(Task.FromResult<FuturesAdxSignalReadModel?>(SampleData.FuturesAdxSignal));

        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateDailyQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesAdxSignalQuery.Verb, Arg.Any<ServiceResult<FuturesAdxSignalReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await marketDataDb.Received(1).GetLastFuturesAdxDailySignalAsync(query.ContractId, query.TimePeriod, query.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAdxSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAdxSignalReadModel?>>(r => r.Success && r.Value == SampleData.FuturesAdxSignal));
    }

    [Fact]
    public async Task ReceiveAsync_GetFuturesAdxSignalQuery_ReturnsNullResult_WhenNoSignalFound()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        marketDataDb.GetLastFuturesAdxSignalAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TradeTimePeriodType>(), Arg.Any<int>())
            .Returns(Task.FromResult<FuturesAdxSignalReadModel?>(null));

        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(query.Subject.ThreadId, GetFuturesAdxSignalQuery.Verb, Arg.Any<ServiceResult<FuturesAdxSignalReadModel?>>())
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAdxSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAdxSignalReadModel?>>(r => r.Success && r.Value == null));
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

        var query = CreateQuery();
        var state = new DummyQueryState { Id = query.Subject.ThreadId };

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(null!, query);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

        var state = new DummyQueryState { Id = new ActorThreadId(ActorType.Query, FuturesAdxSignalQueryActor.ActorName, "test-thread") };
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_ThrowsInvalidOperationException_WhenQueryTypeIsNotRecognized()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

        var state = new DummyQueryState { Id = new ActorThreadId(ActorType.Query, FuturesAdxSignalQueryActor.ActorName, "test-thread") };
        var context = Substitute.For<IQueryActorContext>();
        var unsupportedQuery = Substitute.For<IQuery>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, unsupportedQuery);

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Unable to process");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_GetFuturesAdxSignalQuery_RepliesWithServiceResultContainingErrorDetails()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        ServiceResult<FuturesAdxSignalReadModel?>? capturedResult = null;
        context.ReplyAsync(threadId, GetFuturesAdxSignalQuery.Verb, Arg.Do<ServiceResult<FuturesAdxSignalReadModel?>>(r => capturedResult = r))
            .Returns(ValueTask.CompletedTask);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesAdxSignalQuery.Verb, exception);

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
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var threadId = new ActorThreadId(ActorType.Query, FuturesAdxSignalQueryActor.ActorName, "test-thread");
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
        var actor = _fixture.CreateAdxQueryActor();

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetFuturesAdxSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

        var query = CreateQuery();
        var context = Substitute.For<IQueryActorContext>();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, default, query, GetFuturesAdxSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

        var threadId = new ActorThreadId(ActorType.Query, FuturesAdxSignalQueryActor.ActorName, "test-thread");
        var context = Substitute.For<IQueryActorContext>();
        var exception = new InvalidOperationException("Test exception");

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetFuturesAdxSignalQuery.Verb, exception);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ThrowsArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var actor = _fixture.CreateAdxQueryActor();

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
        var actor = _fixture.CreateAdxQueryActor();

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var context = Substitute.For<IQueryActorContext>();

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesAdxSignalQuery.Verb, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_WhenReplyAsyncThrows_LogsError_AndDoesNotThrow()
    {
        // Arrange
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesAdxSignalQueryActor>>();
        var actor = _fixture.CreateAdxQueryActor(dbFactory, logger);

        var query = CreateQuery();
        var threadId = query.Subject.ThreadId;
        var exception = new InvalidOperationException("Test exception");
        var context = Substitute.For<IQueryActorContext>();

        context.ReplyAsync(threadId, GetFuturesAdxSignalQuery.Verb, Arg.Any<ServiceResult<FuturesAdxSignalReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Reply failed"));

        // Act
        Func<Task> act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetFuturesAdxSignalQuery.Verb, exception);

        // Assert
        await act.Should().NotThrowAsync();
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion
}
