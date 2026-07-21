using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesMacdSignal;

public class FuturesMacdSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesMacdSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesMacdSignalQueryActor : FuturesMacdSignalQueryActor
    {
        public TestableFuturesMacdSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesMacdSignalQueryActor> logger)
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

    public static IEnumerable<object[]> TimePeriods()
    {
        yield return new object[] { TradeTimePeriodType.Daily };
        yield return new object[] { TradeTimePeriodType.Weekly };
        yield return new object[] { TradeTimePeriodType.Monthly };
    }

    private static GetFuturesMacdSignalQuery CreateMacdSignalQuery(TradeTimePeriodType timePeriod)
    {
        var entityId = new FuturesMacdSignalEntityId(SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength);
        return new GetFuturesMacdSignalQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesMacdSignalQuery.Actor, GetFuturesMacdSignalQuery.Verb, entityId.Format())
        };
    }

    private static GetFuturesMacdDailySignalQuery CreateMacdDailySignalQuery(TradeTimePeriodType timePeriod)
    {
        var entityId = new FuturesMacdDailySignalEntityId(SampleData.ContractId, timePeriod, SampleData.PeriodLength);
        return new GetFuturesMacdDailySignalQuery(SampleData.ContractId, timePeriod, SampleData.PeriodLength)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesMacdDailySignalQuery.Actor, GetFuturesMacdDailySignalQuery.Verb, entityId.Format())
        };
    }

    private static FuturesMacdSignalReadModel CreateReadModel(TradeTimePeriodType timePeriod)
        => new(
            contractId: SampleData.ContractId,
            valueDate: SampleData.ValueDate,
            timePeriod: timePeriod,
            periodLength: SampleData.PeriodLength,
            timestamp: new TimeOnly(18, 50, 10),
            futuresPrice: (decimal)SampleData.FuturesPrice,
            macdLine: 1.5,
            signalLine: 1.2,
            histogram: 0.3,
            macd: FuturesTrendDirectionType.UpTrending,
            macdStrength: FuturesTrendDirectionStrengthType.Medium);

    #region ParseMessage Happy Path Tests

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public void ParseMessage_ShouldParseGetFuturesMacdSignalQuery_Successfully(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(timePeriod);
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]> { Subject = query.Subject.ToString(), Data = serializedData };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesMacdSignalQuery>();
        var parsedQuery = result as GetFuturesMacdSignalQuery;
        parsedQuery!.Subject.Should().BeEquivalentTo(query.Subject);
        parsedQuery.ContractId.Should().Be(SampleData.ContractId);
        parsedQuery.TimePeriod.Should().Be(timePeriod);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetFuturesMacdSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public void ParseMessage_ShouldParseGetFuturesMacdDailySignalQuery_Successfully(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdDailySignalQuery(timePeriod);
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]> { Subject = query.Subject.ToString(), Data = serializedData };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesMacdDailySignalQuery>();
        var parsedQuery = result as GetFuturesMacdDailySignalQuery;
        parsedQuery!.Subject.Should().BeEquivalentTo(query.Subject);
        parsedQuery.ContractId.Should().Be(SampleData.ContractId);
        parsedQuery.TimePeriod.Should().Be(timePeriod);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetFuturesMacdDailySignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldExtractThreadIdFromSubject_Correctly()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);
        var message = new NatsMsg<byte[]> { Subject = query.Subject.ToString(), Data = _fixture.DataSerializer.Serialize(query) };

        // Act
        actor.InvokeParseMessage(context, message);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetFuturesMacdSignalQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);
        var message = new NatsMsg<byte[]> { Subject = query.Subject.ToString(), Data = _fixture.DataSerializer.Serialize(query) };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(null!, message);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorTypeIsNotQuery()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new FuturesMacdSignalEntityId(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Daily, SampleData.PeriodLength);
        var invalidSubject = new ActorSubject(ActorType.Command, GetFuturesMacdSignalQuery.Actor, GetFuturesMacdSignalQuery.Verb, entityId.Format());
        var message = new NatsMsg<byte[]> { Subject = invalidSubject.ToString(), Data = Array.Empty<byte>() };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesMacdSignalQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorNameIsIncorrect()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new FuturesMacdSignalEntityId(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Daily, SampleData.PeriodLength);
        var invalidSubject = new ActorSubject(ActorType.Query, "WrongActor", GetFuturesMacdSignalQuery.Verb, entityId.Format());
        var message = new NatsMsg<byte[]> { Subject = invalidSubject.ToString(), Data = Array.Empty<byte>() };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesMacdSignalQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenVerbIsNotInParseMap()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new FuturesMacdSignalEntityId(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Daily, SampleData.PeriodLength);
        var invalidSubject = new ActorSubject(ActorType.Query, FuturesMacdSignalQueryActor.ActorName, "UnknownVerb", entityId.Format());
        var message = new NatsMsg<byte[]> { Subject = invalidSubject.ToString(), Data = Array.Empty<byte>() };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesMacdSignalQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldHandleEmptyEntityId_InSubject()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var subjectWithEmptyEntityId = new ActorSubject(ActorType.Query, GetFuturesMacdSignalQuery.Actor, GetFuturesMacdSignalQuery.Verb, string.Empty);
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily) with { Subject = subjectWithEmptyEntityId };
        var message = new NatsMsg<byte[]> { Subject = subjectWithEmptyEntityId.ToString(), Data = _fixture.DataSerializer.Serialize(query) };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetFuturesMacdSignalQuery>();
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task ReceiveAsync_ShouldProcessGetFuturesMacdSignalQuery_WhenNoDataExists(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateMacdQueryActor(dbFactory: dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(timePeriod);

        db.GetLastFuturesMacdSignalAsync(SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength)
            .Returns((FuturesMacdSignalReadModel?)null);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await db.Received(1).GetLastFuturesMacdSignalAsync(SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesMacdSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesMacdSignalReadModel>>(r => r.Value == null));
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task ReceiveAsync_ShouldProcessGetFuturesMacdSignalQuery_WhenDataExists(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateMacdQueryActor(dbFactory: dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(timePeriod);
        var expected = CreateReadModel(timePeriod);

        db.GetLastFuturesMacdSignalAsync(SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength)
            .Returns(expected);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await db.Received(1).GetLastFuturesMacdSignalAsync(SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesMacdSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesMacdSignalReadModel>>(r => r.Value == expected));
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task ReceiveAsync_ShouldProcessGetFuturesMacdDailySignalQuery_WhenNoDataExists(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateMacdQueryActor(dbFactory: dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdDailySignalQuery(timePeriod);

        db.GetLastFuturesMacdDailySignalAsync(SampleData.ContractId, timePeriod, SampleData.PeriodLength)
            .Returns((FuturesMacdSignalReadModel?)null);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await db.Received(1).GetLastFuturesMacdDailySignalAsync(SampleData.ContractId, timePeriod, SampleData.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesMacdDailySignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesMacdSignalReadModel>>(r => r.Value == null));
    }

    [Theory]
    [MemberData(nameof(TimePeriods))]
    public async Task ReceiveAsync_ShouldProcessGetFuturesMacdDailySignalQuery_WhenDataExists(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = _fixture.CreateMacdQueryActor(dbFactory: dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdDailySignalQuery(timePeriod);
        var expected = CreateReadModel(timePeriod);

        db.GetLastFuturesMacdDailySignalAsync(SampleData.ContractId, timePeriod, SampleData.PeriodLength)
            .Returns(expected);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await db.Received(1).GetLastFuturesMacdDailySignalAsync(SampleData.ContractId, timePeriod, SampleData.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesMacdDailySignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesMacdSignalReadModel>>(r => r.Value == expected));
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(null!, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenStateIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("state");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var state = Substitute.For<IActorState>();

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowInvalidOperationException_WhenQueryTypeIsUnsupported()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var state = Substitute.For<IActorState>();
        var unknownQuery = Substitute.For<IQuery>();
        unknownQuery.Subject.Returns(new ActorSubject(ActorType.Query, FuturesMacdSignalQueryActor.ActorName, "UnknownVerb", "entity"));

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, unknownQuery);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesMacdSignalQueryActor.ActorName} query: *");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldReplyWithTypedServiceResult_ForGetFuturesMacdSignalQuery()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("boom");

        // Act
        await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, GetFuturesMacdSignalQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesMacdSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesMacdSignalReadModel?>>(r => !r.Success && r.ErrorCode == query.ErrorCode && r.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldReplyWithGenericServiceFailed_ForOtherQueryTypes()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdDailySignalQuery(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("daily boom");

        // Act
        await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, GetFuturesMacdDailySignalQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesMacdDailySignalQuery.Verb,
            Arg.Is<ServiceResult<ActorEntityId>>(r => !r.Success && r.ErrorCode == 9999 && r.ErrorMessage == exception.Message));
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("boom");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(null!, query.Subject.ThreadId, query, GetFuturesMacdSignalQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("boom");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, null!, GetFuturesMacdSignalQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("boom");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, null!, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("verb");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenExceptionMessageIsNull()
    {
        // Arrange
        var actor = _fixture.CreateMacdQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, GetFuturesMacdSignalQuery.Verb, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldSwallowInnerException_WhenReplyAsyncFails()
    {
        // Arrange
        var logger = Substitute.For<ILogger<FuturesMacdSignalQueryActor>>();
        var actor = _fixture.CreateMacdQueryActor(logger: logger);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateMacdSignalQuery(TradeTimePeriodType.Daily);
        var exception = new InvalidOperationException("boom");

        context.ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesMacdSignalQuery.Verb,
            Arg.Any<ServiceResult<FuturesMacdSignalReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        // Act
        var act = async () => await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, GetFuturesMacdSignalQuery.Verb, exception);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion
}
