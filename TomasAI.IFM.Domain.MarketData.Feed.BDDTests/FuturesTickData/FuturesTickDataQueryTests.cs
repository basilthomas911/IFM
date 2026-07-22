using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesTickData;

public class FuturesTickDataQueryTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;
    static readonly DateTime TickDate = SampleData.ValueDate.ToDateTime(
        SampleData.EsTickData.TickTime, DateTimeKind.Utc);

    public FuturesTickDataQueryTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("Latest")]
    [InlineData("ByTickDate")]
    public void Given_AValidTickQueryMessage_When_ItIsParsed_Then_TheQueryAndMessageInfoArePreserved(string kind)
    {
        var actor = _fixture.CreateTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery(kind);

        var parsed = actor.InvokeParseMessage(context, CreateMessage(query));

        parsed.GetType().Should().Be(query.GetType());
        parsed.Subject.Should().Be(query.Subject);
        context.Received(1).SetMessageInfo(
            query.Subject.ThreadId,
            query.Subject.Verb,
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void Given_ALatestTickMessage_When_ItIsParsed_Then_ItsSampleDataParametersArePreserved()
    {
        var actor = _fixture.CreateTickQueryActor();
        var query = (GetLastFuturesTickDataQuery)CreateQuery("Latest");

        var parsed = actor.InvokeParseMessage(
            Substitute.For<IQueryActorContext>(), CreateMessage(query));

        var typed = parsed.Should().BeOfType<GetLastFuturesTickDataQuery>().Which;
        typed.ContractId.Should().Be(SampleData.EsTickData.ContractId);
        typed.ValueDate.Should().Be(SampleData.EsTickData.ValueDate);
    }

    [Fact]
    public void Given_ATickDateMessage_When_ItIsParsed_Then_ItsExactTimestampIsPreserved()
    {
        var actor = _fixture.CreateTickQueryActor();
        var query = (GetLastFuturesTickDataByTickDateQuery)CreateQuery("ByTickDate");

        var parsed = actor.InvokeParseMessage(
            Substitute.For<IQueryActorContext>(), CreateMessage(query));

        var typed = parsed.Should().BeOfType<GetLastFuturesTickDataByTickDateQuery>().Which;
        typed.ContractId.Should().Be(SampleData.EsTickData.ContractId);
        typed.TickDate.Should().Be(TickDate);
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesTickDataQueryActor.ActorName, GetLastFuturesTickDataQuery.Verb)]
    [InlineData(ActorType.Query, "WrongActor", GetLastFuturesTickDataQuery.Verb)]
    [InlineData(ActorType.Query, FuturesTickDataQueryActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidTickQuerySubject_When_ItIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateTickQueryActor();
        var query = CreateQuery("Latest");
        var message = new NatsMsg<byte[]>
        {
            Subject = new ActorSubject(actorType, actorName, verb, query.EntityId.Format()).ToString(),
            Data = Serialize(query)
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTickDataQueryActor.ActorName} query from message: *");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_InvalidTickQueryData_When_ItIsParsed_Then_DeserializationFails(bool empty)
    {
        var actor = _fixture.CreateTickQueryActor();
        var query = CreateQuery("Latest");
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = empty ? [] : [0x00, 0x01, 0x02, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_NoContext_When_ATickQueryIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateTickQueryActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateQuery("Latest")));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_TheRequestedLatestTickExists_When_TheQueryIsReceived_Then_ATypedSuccessIsReplied()
    {
        var (factory, database) = CreateDatabase();
        database.GetLastFuturesTickDataAsync(
            SampleData.EsTickData.ContractId, SampleData.ValueDate).Returns(SampleData.EsTickData);
        var actor = _fixture.CreateTickQueryActor(factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetLastFuturesTickDataQuery)CreateQuery("Latest");

        await actor.InvokeReceiveAsync(context, query);

        await database.Received(1).GetLastFuturesTickDataAsync(query.ContractId, query.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(result =>
                result.Success && result.Value == SampleData.EsTickData));
    }

    [Fact]
    public async Task Given_TheRequestedTickDateExists_When_TheQueryIsReceived_Then_ATypedSuccessIsReplied()
    {
        var (factory, database) = CreateDatabase();
        database.GetLastFuturesTickDataByTickDateAsync(
            SampleData.EsTickData.ContractId, TickDate).Returns(SampleData.EsTickData);
        var actor = _fixture.CreateTickQueryActor(factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetLastFuturesTickDataByTickDateQuery)CreateQuery("ByTickDate");

        await actor.InvokeReceiveAsync(context, query);

        await database.Received(1).GetLastFuturesTickDataByTickDateAsync(query.ContractId, query.TickDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesTickDataByTickDateQuery.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(result =>
                result.Success && result.Value == SampleData.EsTickData));
    }

    [Theory]
    [InlineData("Latest")]
    [InlineData("ByTickDate")]
    public async Task Given_NoRequestedTickExists_When_TheQueryIsReceived_Then_ASuccessfulEmptyValueIsReplied(string kind)
    {
        var (factory, _) = CreateDatabase();
        var actor = _fixture.CreateTickQueryActor(factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery(kind, "MISSING");

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(result => result.Success && result.Value == null));
    }

    [Theory]
    [InlineData("Latest")]
    [InlineData("ByTickDate")]
    public async Task Given_TheTickDatabaseFails_When_TheQueryIsReceived_Then_TheFailurePropagates(string kind)
    {
        var (factory, database) = CreateDatabase();
        database.GetLastFuturesTickDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromException<FuturesTickDataV2ReadModel?>(new InvalidOperationException("database failed")));
        database.GetLastFuturesTickDataByTickDateAsync(Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns(Task.FromException<FuturesTickDataV2ReadModel?>(new InvalidOperationException("database failed")));
        var actor = _fixture.CreateTickQueryActor(factory);

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IQueryActorContext>(), CreateQuery(kind)).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database failed");
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_ATickQueryIsReceived_Then_EachIsRejected()
    {
        var actor = _fixture.CreateTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("Latest");

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, query).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedTickQuery_When_ItIsReceived_Then_ItIsRejected()
    {
        var actor = _fixture.CreateTickQueryActor();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesTickDataQueryActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesTickDataQueryActor.ActorName} query: *");
    }

    [Theory]
    [InlineData("Latest")]
    [InlineData("ByTickDate")]
    public async Task Given_AKnownTickQueryFailure_When_ItIsHandled_Then_TheTypedFailureIsReplied(string kind)
    {
        var actor = _fixture.CreateTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery(kind);

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, query.Subject.Verb,
            new TimeoutException("query timed out"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            Arg.Is<ServiceResult<FuturesTickDataV2ReadModel?>>(result =>
                !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == "query timed out"));
    }

    [Fact]
    public async Task Given_AnUnknownTickQueryFailure_When_ItIsHandled_Then_TheFallbackFailureIsReplied()
    {
        var actor = _fixture.CreateTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesTickDataQueryActor.ActorName, "Unknown", "entity"));

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, "Unknown", new Exception("unknown failure"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            "Unknown",
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success && result.ErrorCode == 9999 && result.ErrorMessage == "unknown failure"));
    }

    [Fact]
    public async Task Given_ReplyingToATickFailureAlsoFails_When_ItIsHandled_Then_TheSecondaryFailureIsSwallowed()
    {
        var actor = _fixture.CreateTickQueryActor(logger: Substitute.For<ILogger<FuturesTickDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("Latest");
        context.ReplyAsync(
                Arg.Any<ActorThreadId>(), Arg.Any<string>(),
                Arg.Any<ServiceResult<FuturesTickDataV2ReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        Func<Task> act = () => actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, query.Subject.Verb,
            new Exception("original failure")).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_MissingExceptionInputs_When_ATickFailureIsHandled_Then_EachIsRejected()
    {
        var actor = _fixture.CreateTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("Latest");
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

    static (IDbContextFactory Factory, IMarketDataDbContext Database) CreateDatabase()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var database = Substitute.For<IMarketDataDbContext>();
        factory.MarketDataDb.Returns(database);
        return (factory, database);
    }

    static IQuery CreateQuery(string kind, string? contractId = null)
    {
        var selectedContractId = contractId ?? SampleData.EsTickData.ContractId;
        IQuery query = kind switch
        {
            "Latest" => new GetLastFuturesTickDataQuery(selectedContractId, SampleData.ValueDate),
            "ByTickDate" => new GetLastFuturesTickDataByTickDateQuery(selectedContractId, TickDate),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var subject = new ActorSubject(
            ActorType.Query,
            FuturesTickDataQueryActor.ActorName,
            GetVerb(query),
            query.EntityId.Format());
        switch (query)
        {
            case GetLastFuturesTickDataQuery latest:
                latest.Subject = subject;
                break;
            case GetLastFuturesTickDataByTickDateQuery byTickDate:
                byTickDate.Subject = subject;
                break;
        }
        return query;
    }

    static string GetVerb(IQuery query) => query switch
    {
        GetLastFuturesTickDataQuery => GetLastFuturesTickDataQuery.Verb,
        GetLastFuturesTickDataByTickDateQuery => GetLastFuturesTickDataByTickDateQuery.Verb,
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };

    static NatsMsg<byte[]> CreateMessage(IQuery query)
        => new() { Subject = query.Subject.ToString(), Data = Serialize(query) };

    static byte[] Serialize(IQuery query) => query switch
    {
        GetLastFuturesTickDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetLastFuturesTickDataByTickDateQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };
}
