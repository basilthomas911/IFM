using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesBarData;

public class FuturesBarDataQueryActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesBarDataQueryActorTests(MarketDataFeedTestFixture fixture) => _fixture = fixture;

    public class TestableFuturesBarDataQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesBarDataQueryActor> logger)
        : FuturesBarDataQueryActor(dbFactory, logger)
    {
        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => ReceiveAsync(context, query);

        public ValueTask InvokeOnExceptionAsync(
            IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception exception)
            => OnExceptionAsync(context, threadId, query, verb, exception);
    }

    [Theory]
    [MemberData(nameof(ValidQueries))]
    public void ParseMessage_ValidSupportedQuery_ReturnsConcreteQueryAndStoresMessageInfo(IQuery query)
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FuturesBarDataQueryActor>>();
        var actor = _fixture.CreateActor(dbFactory, logger);
        var context = Substitute.For<IQueryActorContext>();

        var result = actor.InvokeParseMessage(context, CreateMessage(query));

        result.GetType().Should().Be(query.GetType());
        result.Subject.Should().Be(query.Subject);
        context.Received(1).SetMessageInfo(
            query.Subject.ThreadId, query.Subject.Verb, Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_RangeQuery_PreservesOneMinuteSampleWindow()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var query = CreateRangeQuery();

        var result = actor.InvokeParseMessage(
            Substitute.For<IQueryActorContext>(), CreateMessage(query));

        var parsed = result.Should().BeOfType<GetFuturesBarDataQuery>().Which;
        parsed.ContractId.Should().Be(SampleData.FuturesBarData1.ContractId);
        parsed.Symbol.Should().Be(SampleData.FuturesBarData1.Symbol);
        parsed.StartDate.Should().Be(SampleData.FuturesBarWindowStart);
        parsed.EndDate.Should().Be(SampleData.FuturesBarWindowEnd);
        (parsed.EndDate - parsed.StartDate).Should().Be(TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesBarDataQueryActor.ActorName, GetFuturesBarDataQuery.Verb)]
    [InlineData(ActorType.Query, "WrongActor", GetFuturesBarDataQuery.Verb)]
    [InlineData(ActorType.Query, FuturesBarDataQueryActor.ActorName, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ThrowsInvalidOperationException(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var query = CreateRangeQuery();
        var subject = new ActorSubject(actorType, actorName, verb, query.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(query) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateLastQuery()));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidPayload_Throws(bool useEmptyPayload)
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var query = CreateRangeQuery();
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = useEmptyPayload ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public async Task ReceiveAsync_RangeQuery_ReturnsDatabaseRows()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        var logger = Substitute.For<ILogger<FuturesBarDataQueryActor>>();
        dbFactory.MarketDataDb.Returns(db);
        ICollection<FuturesBarDataReadModel> expected = [SampleData.FuturesBarData1];
        db.GetFuturesBarDataAsync(
                SampleData.FuturesBarData1.ContractId,
                SampleData.FuturesBarData1.Symbol,
                SampleData.ValueDate,
                SampleData.FuturesBarWindowStart,
                SampleData.FuturesBarWindowEnd)
            .Returns(expected);
        var actor = _fixture.CreateActor(dbFactory, logger);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery();

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetFuturesBarDataAsync(
            query.ContractId, query.Symbol, query.ValueDate, query.StartDate, query.EndDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel[]>>(result =>
                result.Success && result.Value != null && result.Value.SequenceEqual(expected)));
    }

    [Fact]
    public async Task ReceiveAsync_RangeQueryWithNoRows_ReturnsEmptySuccessfulResult()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesBarDataAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Array.Empty<FuturesBarDataReadModel>());
        var actor = _fixture.CreateActor(
            dbFactory, Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery();

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel[]>>(result =>
                result.Success && result.Value != null && result.Value.Length == 0));
    }

    [Fact]
    public async Task ReceiveAsync_LastQuery_ReturnsLastDatabaseBar()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetLastFuturesBarDataAsync(
                SampleData.FuturesBarData1.ContractId,
                SampleData.FuturesBarData1.Symbol,
                SampleData.ValueDate)
            .Returns(SampleData.FuturesBarData1);
        var actor = _fixture.CreateActor(
            dbFactory, Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery();

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel>>(result =>
                result.Success && result.Value == SampleData.FuturesBarData1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReceiveAsync_DatabaseFailure_Propagates(bool useRangeQuery)
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesBarDataAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns<Task<ICollection<FuturesBarDataReadModel>>>(
                _ => throw new InvalidOperationException("range failed"));
        db.GetLastFuturesBarDataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns<Task<FuturesBarDataReadModel>>(
                _ => throw new InvalidOperationException("last failed"));
        var actor = _fixture.CreateActor(
            dbFactory, Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var query = useRangeQuery ? (IQuery)CreateRangeQuery() : CreateLastQuery();

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery();

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, query).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedQuery_ThrowsInvalidOperationException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesBarDataQueryActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OnExceptionAsync_RangeQuery_RepliesWithFailedRangeResult()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery();

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, GetFuturesBarDataQuery.Verb,
            new TimeoutException("range timed out"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel[]>>(result =>
                !result.Success && result.ErrorCode == query.ErrorCode &&
                result.ErrorMessage == "range timed out"));
    }

    [Fact]
    public async Task OnExceptionAsync_LastQuery_RepliesWithFailedLastResult()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery();

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, GetLastFuturesBarDataQuery.Verb,
            new InvalidOperationException("last failed"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel>>(result =>
                !result.Success && result.ErrorCode == query.ErrorCode &&
                result.ErrorMessage == "last failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_UnknownQuery_RepliesWithFallbackFailure()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesBarDataQueryActor.ActorName, "Unknown", "entity"));

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, "Unknown", new Exception("unknown failed"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            "Unknown",
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success && result.ErrorCode == 9999 && result.ErrorMessage == "unknown failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ReplyFails_SwallowsSecondaryFailure()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery();
        context.ReplyAsync(
                Arg.Any<ActorThreadId>(), Arg.Any<string>(),
                Arg.Any<ServiceResult<FuturesBarDataReadModel[]>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        Func<Task> act = () => actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, GetFuturesBarDataQuery.Verb,
            new Exception("original failure")).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = _fixture.CreateActor(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery();
        var exception = new Exception("failure");

        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                null!, query.Subject.ThreadId, query, GetLastFuturesBarDataQuery.Verb, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, default, query, GetLastFuturesBarDataQuery.Verb, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, query.Subject.ThreadId, null!, GetLastFuturesBarDataQuery.Verb, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, query.Subject.ThreadId, query, null!, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, query.Subject.ThreadId, query, GetLastFuturesBarDataQuery.Verb, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    public static IEnumerable<object[]> ValidQueries()
    {
        yield return [CreateRangeQuery()];
        yield return [CreateLastQuery()];
    }

    static GetFuturesBarDataQuery CreateRangeQuery()
    {
        var entityId = new GetFuturesBarDataParameter(
            SampleData.FuturesBarData1.ContractId,
            SampleData.FuturesBarData1.Symbol,
            SampleData.ValueDate,
            SampleData.FuturesBarWindowStart,
            SampleData.FuturesBarWindowEnd);
        return new GetFuturesBarDataQuery(
            entityId.ContractId, entityId.Symbol, entityId.ValueDate,
            entityId.StartDate, entityId.EndDate)
        {
            Subject = new ActorSubject(
                ActorType.Query, FuturesBarDataQueryActor.ActorName,
                GetFuturesBarDataQuery.Verb, entityId.Format())
        };
    }

    static GetLastFuturesBarDataQuery CreateLastQuery()
    {
        var entityId = new GetLastFuturesBarDataParameter(
            SampleData.FuturesBarData1.ContractId,
            SampleData.FuturesBarData1.Symbol,
            SampleData.ValueDate);
        return new GetLastFuturesBarDataQuery(
            entityId.ContractId, entityId.Symbol, entityId.ValueDate)
        {
            Subject = new ActorSubject(
                ActorType.Query, FuturesBarDataQueryActor.ActorName,
                GetLastFuturesBarDataQuery.Verb, entityId.Format())
        };
    }

    static NatsMsg<byte[]> CreateMessage(IQuery query)
        => new() { Subject = query.Subject.ToString(), Data = Serialize(query) };

    static byte[] Serialize(IQuery query)
        => query switch
        {
            GetFuturesBarDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
            GetLastFuturesBarDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };
}
