using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesOptionTickData;

public class FuturesOptionTickDataQueryTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesOptionTickDataQueryTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Fact]
    public void Given_AValidLastOptionTickMessage_When_ItIsParsed_Then_TheQueryAndMessageInfoArePreserved()
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery();

        var parsed = actor.InvokeParseMessage(context, CreateMessage(query));

        var typed = parsed.Should().BeOfType<GetLastFuturesOptionTickDataQuery>().Which;
        typed.ContractId.Should().Be(SampleData.EsOptionTickData.ContractId);
        typed.ValueDate.Should().Be(SampleData.ValueDate);
        context.Received(1).SetMessageInfo(
            query.Subject.ThreadId,
            GetLastFuturesOptionTickDataQuery.Verb,
            Arg.Any<ActorMessageInfo>());
    }

    [Theory]
    [InlineData(ActorType.Command, FuturesOptionTickDataQueryActor.ActorName, GetLastFuturesOptionTickDataQuery.Verb)]
    [InlineData(ActorType.Query, "WrongActor", GetLastFuturesOptionTickDataQuery.Verb)]
    [InlineData(ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidOptionTickQuerySubject_When_ItIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var query = CreateQuery();
        var message = new NatsMsg<byte[]>
        {
            Subject = new ActorSubject(actorType, actorName, verb, query.EntityId.Format()).ToString(),
            Data = Serialize(query)
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesOptionTickDataQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void Given_CorruptOptionTickQueryData_When_ItIsParsed_Then_DeserializationFails()
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var query = CreateQuery();
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = [0x00, 0x01, 0x02, 0xFF]
        };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_NoContext_When_AnOptionTickQueryIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateOptionTickQueryActor();

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(CreateQuery()));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_TheRequestedOptionTickExists_When_TheQueryIsReceived_Then_ATypedSuccessIsReplied()
    {
        var (factory, database) = CreateDatabase(SampleData.EsOptionTickData);
        var actor = _fixture.CreateOptionTickQueryActor(factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery();

        await actor.InvokeReceiveAsync(context, query);

        await database.Received(1).GetLastFuturesOptionTickDataAsync(query.ContractId, query.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesOptionTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionTickDataV2ReadModel?>>(result =>
                result.Success && result.Value == SampleData.EsOptionTickData));
    }

    [Fact]
    public async Task Given_NoRequestedOptionTickExists_When_TheQueryIsReceived_Then_ASuccessfulEmptyValueIsReplied()
    {
        var (factory, database) = CreateDatabase(null);
        var actor = _fixture.CreateOptionTickQueryActor(factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("MISSING");

        await actor.InvokeReceiveAsync(context, query);

        await database.Received(1).GetLastFuturesOptionTickDataAsync("MISSING", SampleData.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesOptionTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionTickDataV2ReadModel?>>(result => result.Success && result.Value == null));
    }

    [Fact]
    public async Task Given_TheOptionTickDatabaseFails_When_TheQueryIsReceived_Then_TheFailurePropagates()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var database = Substitute.For<IMarketDataDbContext>();
        factory.MarketDataDb.Returns(database);
        database.GetLastFuturesOptionTickDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns<Task<FuturesOptionTickDataV2ReadModel?>>(_ => throw new InvalidOperationException("database failed"));
        var actor = _fixture.CreateOptionTickQueryActor(factory);

        Func<Task> act = () => actor.InvokeReceiveAsync(
            Substitute.For<IQueryActorContext>(), CreateQuery()).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database failed");
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_AnOptionTickQueryIsReceived_Then_EachIsRejected()
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery();

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, query).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedOptionTickQuery_When_ItIsReceived_Then_ItIsRejected()
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesOptionTickDataQueryActor.ActorName} query: *");
    }

    [Fact]
    public async Task Given_AKnownOptionTickQueryFailure_When_ItIsHandled_Then_TheTypedFailureIsReplied()
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery();

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, GetLastFuturesOptionTickDataQuery.Verb,
            new TimeoutException("query timed out"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesOptionTickDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionTickDataV2ReadModel?>>(result =>
                !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == "query timed out"));
    }

    [Fact]
    public async Task Given_AnUnknownQueryFailure_When_ItIsHandled_Then_TheFallbackFailureIsReplied()
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(
            ActorType.Query, FuturesOptionTickDataQueryActor.ActorName, "Unknown", "entity"));

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, "Unknown", new Exception("unknown failure"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            "Unknown",
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success && result.ErrorCode == 9999 && result.ErrorMessage == "unknown failure"));
    }

    [Fact]
    public async Task Given_ReplyingToAnOptionTickFailureAlsoFails_When_ItIsHandled_Then_TheSecondaryFailureIsSwallowed()
    {
        var actor = _fixture.CreateOptionTickQueryActor(logger: Substitute.For<ILogger<FuturesOptionTickDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery();
        context.ReplyAsync(
                Arg.Any<ActorThreadId>(), Arg.Any<string>(),
                Arg.Any<ServiceResult<FuturesOptionTickDataV2ReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        Func<Task> act = () => actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, query.Subject.Verb,
            new Exception("original failure")).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_MissingExceptionInputs_When_AnOptionTickFailureIsHandled_Then_EachIsRejected()
    {
        var actor = _fixture.CreateOptionTickQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery();
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

    static (IDbContextFactory Factory, IMarketDataDbContext Database) CreateDatabase(
        FuturesOptionTickDataV2ReadModel? result)
    {
        var factory = Substitute.For<IDbContextFactory>();
        var database = Substitute.For<IMarketDataDbContext>();
        factory.MarketDataDb.Returns(database);
        database.GetLastFuturesOptionTickDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(result);
        return (factory, database);
    }

    static GetLastFuturesOptionTickDataQuery CreateQuery(string? contractId = null)
    {
        var query = new GetLastFuturesOptionTickDataQuery(
            contractId ?? SampleData.EsOptionTickData.ContractId,
            SampleData.ValueDate);
        query.Subject = new ActorSubject(
            ActorType.Query,
            FuturesOptionTickDataQueryActor.ActorName,
            GetLastFuturesOptionTickDataQuery.Verb,
            query.EntityId.Format());
        return query;
    }

    static NatsMsg<byte[]> CreateMessage(GetLastFuturesOptionTickDataQuery query)
        => new() { Subject = query.Subject.ToString(), Data = Serialize(query) };

    static byte[] Serialize(GetLastFuturesOptionTickDataQuery query)
        => ActorExtensions.DataSerializer!.Serialize(query);
}
