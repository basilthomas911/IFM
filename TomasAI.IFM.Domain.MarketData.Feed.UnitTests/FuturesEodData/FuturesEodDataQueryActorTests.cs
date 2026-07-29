using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.FuturesEodData;

public class FuturesEodDataQueryActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public FuturesEodDataQueryActorTests(MarketDataFeedTestFixture fixture) => _fixture = fixture;

    public class TestableFuturesEodDataQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesEodDataQueryActor> logger)
        : FuturesEodDataQueryActor(dbFactory, logger)
    {
        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => ReceiveAsync(context, query);

        public ValueTask InvokeOnExceptionAsync(
            IQueryActorContext context, ActorThreadId threadId, IQuery query,
            string verb, Exception exception)
            => OnExceptionAsync(context, threadId, query, verb, exception);
    }

    public static IEnumerable<object[]> SupportedQueries()
    {
        yield return [CreateQuery("Range"), GetFuturesEodDataByDateRangeQuery.Verb];
        yield return [CreateQuery("Parameters"), GetFuturesEodDataParametersQuery.Verb];
        yield return [CreateQuery("Current"), GetFuturesEodDataQuery.Verb];
        yield return [CreateQuery("Last"), GetLastFuturesEodDataQuery.Verb];
        yield return [CreateQuery("MovingAverages"), GetFuturesEodDataMovingAveragesQuery.Verb];
        yield return [CreateQuery("LastVix"), GetLastVixFuturesEodDataQuery.Verb];
        yield return [CreateQuery("Vix"), GetVixFuturesEodDataQuery.Verb];
    }

    [Theory]
    [MemberData(nameof(SupportedQueries))]
    public void ParseMessage_ValidSupportedQuery_ReturnsConcreteQueryAndStoresMessageInfo(
        IQuery query, string verb)
    {
        var actor = CreateActor();
        var context = Substitute.For<IQueryActorContext>();

        var parsed = actor.InvokeParseMessage(context, CreateMessage(query));

        parsed.GetType().Should().Be(query.GetType());
        parsed.Subject.Should().Be(query.Subject);
        parsed.EntityId.Format().Should().Be(query.EntityId.Format());
        context.Received(1).SetMessageInfo(
            query.Subject.ThreadId, verb, Arg.Any<ActorMessageInfo>());
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesEodDataQueryActor.ActorName, GetFuturesEodDataQuery.Verb)]
    [InlineData(ActorType.Query, "WrongActor", GetFuturesEodDataQuery.Verb)]
    [InlineData(ActorType.Query, FuturesEodDataQueryActor.ActorName, "UnknownVerb")]
    public void ParseMessage_InvalidSubject_ThrowsInvalidOperationException(
        ActorType actorType, string actorName, string verb)
    {
        var actor = CreateActor();
        var query = CreateQuery("Current");
        var subject = new ActorSubject(actorType, actorName, verb, query.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(query) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataQueryActor.ActorName} query from message: *");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseMessage_InvalidPayload_Throws(bool emptyPayload)
    {
        var actor = CreateActor();
        var query = CreateQuery("Current");
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = emptyPayload ? [] : [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var actor = CreateActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateQuery("Current")));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(SupportedQueries))]
    public async Task ReceiveAsync_SupportedQuery_RepliesWithSuccessfulTypedResult(
        IQuery query, string requestedVerb)
    {
        var (dbFactory, _) = CreateDatabaseWithResults();
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();

        await actor.InvokeReceiveAsync(context, query);

        await VerifySuccessfulReply(context, query, requestedVerb);
    }

    [Fact]
    public async Task ReceiveAsync_CurrentQuery_EnrichesResultWithMovingAverages()
    {
        var (dbFactory, db) = CreateDatabaseWithResults();
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesEodDataQuery)CreateQuery("Current");
        var expected = SampleData.EodClosingPrices.Average(value => value.ClosingPrice);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetFuturesEodClosingPricesAsync(
            query.ContractId, SampleData.Symbol, query.ValueDate.AddYears(-1), query.ValueDate, 50);
        await db.Received(1).GetFuturesEodClosingPricesAsync(
            query.ContractId, SampleData.Symbol, query.ValueDate.AddYears(-1), query.ValueDate, 200);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesEodDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesEodDataV2ReadModel>>(result =>
                result.Success && result.Value != null &&
                result.Value.FiftyDMA == expected && result.Value.TwoHundredDMA == expected));
    }

    [Fact]
    public async Task ReceiveAsync_VixQueryWithoutContract_ReturnsAllContractsForValueDate()
    {
        var (dbFactory, db) = CreateDatabaseWithResults();
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateVixQuery(string.Empty);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetVixFuturesEodDataByValueDateAsync(SampleData.ValueDate);
        await db.DidNotReceive().GetVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>());
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetVixFuturesEodDataQuery.Verb,
            Arg.Is<ServiceResult<VixFuturesEodDataReadModel[]>>(result =>
                result.Success && result.Value != null &&
                result.Value.SequenceEqual(SampleData.VixEodData)));
    }

    [Fact]
    public async Task ReceiveAsync_CurrentQueryWithNoRow_RepliesWithSuccessfulNullValue()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns((FuturesEodDataV2ReadModel?)null);
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesEodDataQuery)CreateQuery("Current");

        await actor.InvokeReceiveAsync(context, query);

        await db.DidNotReceive().GetFuturesEodClosingPricesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<int>());
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesEodDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesEodDataV2ReadModel>>(result =>
                result.Success && result.Value == null));
    }

    [Fact]
    public async Task ReceiveAsync_MovingAveragesWithNoHistory_ReturnsZeroAverages()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesEodClosingPricesAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<int>())
            .Returns(Array.Empty<FuturesEodClosingPriceReadModel>());
        var actor = CreateActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesEodDataMovingAveragesQuery)CreateQuery("MovingAverages");

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesEodDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesEodDataMovingAveragesReadModel>>(result =>
                result.Success && result.Value != null &&
                result.Value.FiftyDMA == 0 && result.Value.TwoHundredDMA == 0));
    }

    [Fact]
    public async Task ReceiveAsync_DatabaseFailure_PropagatesException()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetLastVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns<Task<VixFuturesEodDataReadModel?>>(
                _ => throw new InvalidOperationException("database failed"));
        var actor = CreateActor(dbFactory);

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IQueryActorContext>(), CreateQuery("LastVix")).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database failed");
    }

    [Fact]
    public async Task ReceiveAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("Current");

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, query).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedQuery_ThrowsInvalidOperationException()
    {
        var actor = CreateActor();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesEodDataQueryActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesEodDataQueryActor.ActorName} query: *");
    }

    [Theory]
    [MemberData(nameof(SupportedQueries))]
    public async Task OnExceptionAsync_KnownQuery_RepliesWithMatchingTypedFailure(
        IQuery query, string verb)
    {
        var actor = CreateActor();
        var context = Substitute.For<IQueryActorContext>();

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, verb, new TimeoutException("query timed out"));

        await VerifyFailedReply(context, query, verb, "query timed out");
    }

    [Fact]
    public async Task OnExceptionAsync_UnknownQuery_RepliesWithFallbackFailure()
    {
        var actor = CreateActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesEodDataQueryActor.ActorName, "Unknown", "entity"));

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, "Unknown", new Exception("unknown failure"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            "Unknown",
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success && result.ErrorCode == 9999 && result.ErrorMessage == "unknown failure"));
    }

    [Fact]
    public async Task OnExceptionAsync_ReplyFailure_IsLoggedAndSwallowed()
    {
        var logger = Substitute.For<ILogger<FuturesEodDataQueryActor>>();
        var actor = CreateActor(logger: logger);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesEodDataByDateRangeQuery)CreateQuery("Range");
        context.ReplyAsync(
                Arg.Any<ActorThreadId>(), Arg.Any<string>(),
                Arg.Any<ServiceResult<FuturesEodDataV2ReadModel[]>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        Func<Task> act = () => actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, query.Subject.Verb,
            new Exception("original failure")).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnExceptionAsync_NullInputs_ThrowArgumentNullException()
    {
        var actor = CreateActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("Current");
        var exception = new Exception("failure");

        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                null!, query.Subject.ThreadId, query, query.Subject.Verb, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, default, query, query.Subject.Verb, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, query.Subject.ThreadId, null!, query.Subject.Verb, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, query.Subject.ThreadId, query, null!, exception).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeOnExceptionAsync(
                context, query.Subject.ThreadId, query, query.Subject.Verb, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    TestableFuturesEodDataQueryActor CreateActor(
        IDbContextFactory? dbFactory = null,
        ILogger<FuturesEodDataQueryActor>? logger = null)
        => _fixture.CreateActor(
            dbFactory ?? Substitute.For<IDbContextFactory>(),
            logger ?? Substitute.For<ILogger<FuturesEodDataQueryActor>>());

    static (IDbContextFactory Factory, IMarketDataDbContext Database) CreateDatabaseWithResults()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(SampleData.EodDataToday);
        db.GetLastFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(SampleData.EodDataToday);
        db.GetFuturesEodDataByDateRangeAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(SampleData.EodDataRange);
        db.GetNormalCurveTableAsync().Returns(SampleData.NormCurveData);
        db.GetFuturesEodClosingPricesAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<int>())
            .Returns(SampleData.EodClosingPrices);
        db.GetLastVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(SampleData.VixEodData[0]);
        db.GetVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(SampleData.VixEodData[0]);
        db.GetVixFuturesEodDataByValueDateAsync(Arg.Any<DateOnly>())
            .Returns(SampleData.VixEodData);
        return (dbFactory, db);
    }

    static async Task VerifySuccessfulReply(
        IQueryActorContext context, IQuery query, string requestedVerb)
    {
        var replyVerb = query is GetFuturesEodDataByDateRangeQuery or GetVixFuturesEodDataQuery
            ? requestedVerb
            : GetFuturesEodDataQuery.Verb;
        switch (query)
        {
            case GetFuturesEodDataByDateRangeQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<FuturesEodDataV2ReadModel[]>>(result =>
                        result.Success && result.Value != null &&
                        result.Value.Length == SampleData.EodDataRange.Length));
                break;
            case GetFuturesEodDataParametersQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<FuturesEodDataParametersReadModel>>(result =>
                        result.Success && result.Value != null &&
                        result.Value.FuturesEodDataToday == SampleData.EodDataToday));
                break;
            case GetFuturesEodDataQuery:
            case GetLastFuturesEodDataQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<FuturesEodDataV2ReadModel>>(result =>
                        result.Success && result.Value != null &&
                        result.Value.ContractId == SampleData.EodDataToday.ContractId));
                break;
            case GetFuturesEodDataMovingAveragesQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<FuturesEodDataMovingAveragesReadModel>>(result => result.Success));
                break;
            case GetLastVixFuturesEodDataQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<VixFuturesEodDataReadModel>>(result => result.Success));
                break;
            case GetVixFuturesEodDataQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<VixFuturesEodDataReadModel[]>>(result =>
                        result.Success && result.Value != null && result.Value.Length == 1));
                break;
        }
    }

    static async Task VerifyFailedReply(
        IQueryActorContext context, IQuery query, string verb, string errorMessage)
    {
        switch (query)
        {
            case GetFuturesEodDataByDateRangeQuery:
                await Verify<FuturesEodDataV2ReadModel[]>(); break;
            case GetFuturesEodDataParametersQuery:
                await Verify<FuturesEodDataParametersReadModel>(); break;
            case GetFuturesEodDataQuery:
            case GetLastFuturesEodDataQuery:
                await Verify<FuturesEodDataV2ReadModel>(); break;
            case GetFuturesEodDataMovingAveragesQuery:
                await Verify<FuturesEodDataMovingAveragesReadModel>(); break;
            case GetLastVixFuturesEodDataQuery:
                await Verify<VixFuturesEodDataReadModel>(); break;
            case GetVixFuturesEodDataQuery:
                await Verify<VixFuturesEodDataReadModel[]>(); break;
        }

        async Task Verify<TResult>() where TResult : class
            => await context.Received(1).ReplyAsync(
                query.Subject.ThreadId,
                verb,
                Arg.Is<ServiceResult<TResult>>(result =>
                    !result.Success && result.ErrorCode == query.ErrorCode &&
                    result.ErrorMessage == errorMessage));
    }

    static IQuery CreateQuery(string kind)
    {
        IQuery query = kind switch
        {
            "Range" => new GetFuturesEodDataByDateRangeQuery(
                SampleData.EodDataToday.ContractId,
                SampleData.ValueDate.AddMonths(-1), SampleData.ValueDate),
            "Parameters" => new GetFuturesEodDataParametersQuery(
                SampleData.EodDataToday.ContractId, SampleData.ValueDate),
            "Current" => new GetFuturesEodDataQuery(
                SampleData.EodDataToday.ContractId, SampleData.ValueDate),
            "Last" => new GetLastFuturesEodDataQuery(
                SampleData.EodDataToday.ContractId, SampleData.ValueDate),
            "MovingAverages" => new GetFuturesEodDataMovingAveragesQuery(
                SampleData.EodDataToday.ContractId, SampleData.Symbol, SampleData.ValueDate),
            "LastVix" => new GetLastVixFuturesEodDataQuery("VX", SampleData.ValueDate),
            "Vix" => new GetVixFuturesEodDataQuery("VX", SampleData.ValueDate),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        SetSubject(query, new ActorSubject(
            ActorType.Query, FuturesEodDataQueryActor.ActorName,
            GetVerb(query), query.EntityId.Format()));
        return query;
    }

    static GetVixFuturesEodDataQuery CreateVixQuery(string contractId)
    {
        var query = new GetVixFuturesEodDataQuery(contractId, SampleData.ValueDate);
        query.Subject = new ActorSubject(
            ActorType.Query, FuturesEodDataQueryActor.ActorName,
            GetVixFuturesEodDataQuery.Verb, query.EntityId.Format());
        return query;
    }

    static string GetVerb(IQuery query) => query switch
    {
        GetFuturesEodDataByDateRangeQuery => GetFuturesEodDataByDateRangeQuery.Verb,
        GetFuturesEodDataParametersQuery => GetFuturesEodDataParametersQuery.Verb,
        GetFuturesEodDataQuery => GetFuturesEodDataQuery.Verb,
        GetLastFuturesEodDataQuery => GetLastFuturesEodDataQuery.Verb,
        GetFuturesEodDataMovingAveragesQuery => GetFuturesEodDataMovingAveragesQuery.Verb,
        GetLastVixFuturesEodDataQuery => GetLastVixFuturesEodDataQuery.Verb,
        GetVixFuturesEodDataQuery => GetVixFuturesEodDataQuery.Verb,
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };

    static void SetSubject(IQuery query, ActorSubject subject)
    {
        switch (query)
        {
            case GetFuturesEodDataByDateRangeQuery value: value.Subject = subject; break;
            case GetFuturesEodDataParametersQuery value: value.Subject = subject; break;
            case GetFuturesEodDataQuery value: value.Subject = subject; break;
            case GetLastFuturesEodDataQuery value: value.Subject = subject; break;
            case GetFuturesEodDataMovingAveragesQuery value: value.Subject = subject; break;
            case GetLastVixFuturesEodDataQuery value: value.Subject = subject; break;
            case GetVixFuturesEodDataQuery value: value.Subject = subject; break;
            default: throw new ArgumentOutOfRangeException(nameof(query));
        }
    }

    static NatsMsg<byte[]> CreateMessage(IQuery query)
        => new() { Subject = query.Subject.ToString(), Data = Serialize(query) };

    static byte[] Serialize(IQuery query) => query switch
    {
        GetFuturesEodDataByDateRangeQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetFuturesEodDataParametersQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetFuturesEodDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetLastFuturesEodDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetFuturesEodDataMovingAveragesQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetLastVixFuturesEodDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetVixFuturesEodDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };
}
