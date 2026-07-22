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
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesEodData;

public class FuturesEodDataQueryTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesEodDataQueryTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

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
    public void Given_AValidSupportedEodQueryMessage_When_ItIsParsed_Then_TheQueryAndMessageInfoArePreserved(
        IQuery query, string verb)
    {
        var actor = _fixture.CreateEodQueryActor();
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
    public void Given_AnInvalidEodQuerySubject_When_ItIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateEodQueryActor();
        var query = CreateQuery("Current");
        var subject = new ActorSubject(actorType, actorName, verb, query.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(query) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesEodDataQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void Given_CorruptEodQueryData_When_ItIsParsed_Then_DeserializationFails()
    {
        var actor = _fixture.CreateEodQueryActor();
        var query = CreateQuery("Current");
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = [0x00, 0x01, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_NoQueryContext_When_AnEodMessageIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateEodQueryActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateQuery("Current")));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(SupportedQueries))]
    public async Task Given_TheRequestedEodDataExists_When_AQueryIsReceived_Then_ASuccessfulTypedResultIsReplied(
        IQuery query, string verb)
    {
        var (dbFactory, _) = CreateDatabaseWithResults();
        var actor = _fixture.CreateEodQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();

        await actor.InvokeReceiveAsync(context, query);

        await VerifySuccessfulReply(context, query, verb);
    }

    [Fact]
    public async Task Given_NoContractId_When_A_VixQueryIsReceived_Then_AllContractsForTheDateAreReturned()
    {
        var (dbFactory, db) = CreateDatabaseWithResults();
        var actor = _fixture.CreateEodQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateVixQuery(string.Empty);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetVixFuturesEodDataByValueDateAsync(SampleData.ValueDate);
        await db.DidNotReceive().GetVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>());
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetVixFuturesEodDataQuery.Verb,
            Arg.Is<ServiceResult<VixFuturesEodDataReadModel[]>>(result =>
                result.Success && result.Value != null && result.Value.Length == SampleData.VixEodData.Length));
    }

    [Fact]
    public async Task Given_NoCurrentEodRow_When_TheCurrentQueryIsReceived_Then_ASuccessfulEmptyValueIsReplied()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns((FuturesEodDataV2ReadModel?)null);
        var actor = _fixture.CreateEodQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesEodDataQuery)CreateQuery("Current");

        await actor.InvokeReceiveAsync(context, query);

        await db.DidNotReceive().GetFuturesEodClosingPricesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<int>());
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesEodDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesEodDataV2ReadModel>>(result => result.Success && result.Value == null));
    }

    [Fact]
    public async Task Given_ClosingPriceHistory_When_MovingAveragesAreRequested_Then_TheAveragesAreCalculated()
    {
        var (dbFactory, db) = CreateDatabaseWithResults();
        var actor = _fixture.CreateEodQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesEodDataMovingAveragesQuery)CreateQuery("MovingAverages");
        var expected = SampleData.EodClosingPrices.Average(value => value.ClosingPrice);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetFuturesEodClosingPricesAsync(
            query.ContractId, query.Symbol, query.ValueDate.AddYears(-1), query.ValueDate, 50);
        await db.Received(1).GetFuturesEodClosingPricesAsync(
            query.ContractId, query.Symbol, query.ValueDate.AddYears(-1), query.ValueDate, 200);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesEodDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesEodDataMovingAveragesReadModel>>(result =>
                result.Success && result.Value != null &&
                result.Value.FiftyDMA == expected && result.Value.TwoHundredDMA == expected));
    }

    [Fact]
    public async Task Given_TheEodDatabaseFails_When_AQueryIsReceived_Then_TheFailurePropagates()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetLastVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns<Task<VixFuturesEodDataReadModel?>>(_ => throw new InvalidOperationException("database failed"));
        var actor = _fixture.CreateEodQueryActor(dbFactory);

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IQueryActorContext>(), CreateQuery("LastVix")).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database failed");
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_AnEodQueryIsReceived_Then_EachMissingInputIsRejected()
    {
        var actor = _fixture.CreateEodQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("Current");

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, query).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedEodQuery_When_ItIsReceived_Then_ItIsRejected()
    {
        var actor = _fixture.CreateEodQueryActor();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesEodDataQueryActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesEodDataQueryActor.ActorName} query: *");
    }

    [Theory]
    [MemberData(nameof(SupportedQueries))]
    public async Task Given_AKnownEodQueryFailure_When_ItIsHandled_Then_TheMatchingTypedFailureIsReplied(
        IQuery query, string verb)
    {
        var actor = _fixture.CreateEodQueryActor();
        var context = Substitute.For<IQueryActorContext>();

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, verb, new TimeoutException("query timed out"));

        await VerifyFailedReply(context, query, verb, "query timed out");
    }

    [Fact]
    public async Task Given_AnUnknownQueryFailure_When_ItIsHandled_Then_TheFallbackFailureIsReplied()
    {
        var actor = _fixture.CreateEodQueryActor();
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
    public async Task Given_ReplyingToAnEodFailureAlsoFails_When_ItIsHandled_Then_TheSecondaryFailureIsSwallowed()
    {
        var actor = _fixture.CreateEodQueryActor(logger: Substitute.For<ILogger<FuturesEodDataQueryActor>>());
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
    public async Task Given_MissingExceptionInputs_When_AnEodFailureIsHandled_Then_EachMissingInputIsRejected()
    {
        var actor = _fixture.CreateEodQueryActor();
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

    static (IDbContextFactory Factory, IMarketDataDbContext Database) CreateDatabaseWithResults()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(SampleData.QueryableEodDataToday);
        db.GetLastFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(SampleData.QueryableEodDataToday);
        db.GetFuturesEodDataByDateRangeAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(SampleData.EodDataRange);
        db.GetNormalCurveTableAsync().Returns(SampleData.NormCurveData);
        db.GetFuturesEodClosingPricesAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<int>())
            .Returns(SampleData.EodClosingPrices);
        db.GetLastVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(SampleData.VixEodDataToday);
        db.GetVixFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(SampleData.VixEodDataToday);
        db.GetVixFuturesEodDataByValueDateAsync(Arg.Any<DateOnly>())
            .Returns(SampleData.VixEodData);
        return (dbFactory, db);
    }

    static async Task VerifySuccessfulReply(IQueryActorContext context, IQuery query, string requestedVerb)
    {
        var replyVerb = query is GetFuturesEodDataByDateRangeQuery or GetVixFuturesEodDataQuery
            ? requestedVerb
            : GetFuturesEodDataQuery.Verb;

        switch (query)
        {
            case GetFuturesEodDataByDateRangeQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<FuturesEodDataV2ReadModel[]>>(result =>
                        result.Success && result.Value != null && result.Value.Length == SampleData.EodDataRange.Length));
                break;
            case GetFuturesEodDataParametersQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<FuturesEodDataParametersReadModel>>(result =>
                        result.Success && result.Value != null && result.Value.FuturesEodDataToday == SampleData.QueryableEodDataToday));
                break;
            case GetFuturesEodDataQuery:
            case GetLastFuturesEodDataQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, replyVerb,
                    Arg.Is<ServiceResult<FuturesEodDataV2ReadModel>>(result =>
                        result.Success && result.Value != null && result.Value.ContractId == SampleData.QueryableEodDataToday.ContractId));
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
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, verb,
                    Arg.Is<ServiceResult<FuturesEodDataV2ReadModel[]>>(result =>
                        !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == errorMessage));
                break;
            case GetFuturesEodDataParametersQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, verb,
                    Arg.Is<ServiceResult<FuturesEodDataParametersReadModel>>(result =>
                        !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == errorMessage));
                break;
            case GetFuturesEodDataQuery:
            case GetLastFuturesEodDataQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, verb,
                    Arg.Is<ServiceResult<FuturesEodDataV2ReadModel>>(result =>
                        !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == errorMessage));
                break;
            case GetFuturesEodDataMovingAveragesQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, verb,
                    Arg.Is<ServiceResult<FuturesEodDataMovingAveragesReadModel>>(result =>
                        !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == errorMessage));
                break;
            case GetLastVixFuturesEodDataQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, verb,
                    Arg.Is<ServiceResult<VixFuturesEodDataReadModel>>(result =>
                        !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == errorMessage));
                break;
            case GetVixFuturesEodDataQuery:
                await context.Received(1).ReplyAsync(query.Subject.ThreadId, verb,
                    Arg.Is<ServiceResult<VixFuturesEodDataReadModel[]>>(result =>
                        !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == errorMessage));
                break;
        }
    }

    static IQuery CreateQuery(string kind)
    {
        IQuery query = kind switch
        {
            "Range" => new GetFuturesEodDataByDateRangeQuery(
                "ES20250620", SampleData.ValueDate.AddMonths(-1), SampleData.ValueDate),
            "Parameters" => new GetFuturesEodDataParametersQuery("ES20250620", SampleData.ValueDate),
            "Current" => new GetFuturesEodDataQuery("ES20250620", SampleData.ValueDate),
            "Last" => new GetLastFuturesEodDataQuery("ES20250620", SampleData.ValueDate),
            "MovingAverages" => new GetFuturesEodDataMovingAveragesQuery("ES20250620", "ES", SampleData.ValueDate),
            "LastVix" => new GetLastVixFuturesEodDataQuery("VX", SampleData.ValueDate),
            "Vix" => new GetVixFuturesEodDataQuery("VX", SampleData.ValueDate),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        SetSubject(query, new ActorSubject(
            ActorType.Query, FuturesEodDataQueryActor.ActorName, GetVerb(query), query.EntityId.Format()));
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
