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

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.FuturesBarData;

public class FuturesBarDataQueryTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public FuturesBarDataQueryTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    [Theory]
    [MemberData(nameof(SampleData.FuturesBarDataCases), MemberType = typeof(SampleData))]
    public void Given_AValidOneMinuteRange_When_TheQueryMessageIsParsed_Then_AllQueryValuesAndMessageInfoArePreserved(
        FuturesBarDataReadModel barData, DateTime windowStart, DateTime windowEnd)
    {
        (windowEnd - windowStart).Should().Be(TimeSpan.FromMinutes(1));
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery(barData, windowStart, windowEnd);

        var parsed = actor.InvokeParseMessage(context, CreateMessage(query));

        parsed.Should().BeOfType<GetFuturesBarDataQuery>()
            .Which.Should().BeEquivalentTo(query);
        context.Received(1).SetMessageInfo(
            query.Subject.ThreadId,
            GetFuturesBarDataQuery.Verb,
            Arg.Any<ActorMessageInfo>());
    }

    [Theory]
    [MemberData(nameof(SampleData.FuturesBarDataCases), MemberType = typeof(SampleData))]
    public void Given_AValidLastBarQuery_When_TheMessageIsParsed_Then_TheLastQueryIsReturned(
        FuturesBarDataReadModel barData, DateTime windowStart, DateTime windowEnd)
    {
        (windowEnd - windowStart).Should().Be(TimeSpan.FromMinutes(1));
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery(barData);

        var parsed = actor.InvokeParseMessage(context, CreateMessage(query));

        parsed.Should().BeOfType<GetLastFuturesBarDataQuery>()
            .Which.Should().BeEquivalentTo(query);
        context.Received(1).SetMessageInfo(
            query.Subject.ThreadId,
            GetLastFuturesBarDataQuery.Verb,
            Arg.Any<ActorMessageInfo>());
    }

    [Theory]
    [InlineData(ActorType.Event, FuturesBarDataQueryActor.ActorName, GetFuturesBarDataQuery.Verb)]
    [InlineData(ActorType.Query, "WrongActor", GetFuturesBarDataQuery.Verb)]
    [InlineData(ActorType.Query, FuturesBarDataQueryActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidQuerySubject_When_TheMessageIsParsed_Then_ItIsRejected(
        ActorType actorType, string actorName, string verb)
    {
        var actor = _fixture.CreateQueryActor();
        var query = CreateRangeQuery(
            SampleData.FuturesBarData,
            SampleData.FuturesBarData.BarDate,
            SampleData.FuturesBarData.BarDate.AddMinutes(1));
        var subject = new ActorSubject(actorType, actorName, verb, query.EntityId.Format());
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = Serialize(query) };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesBarDataQueryActor.ActorName} query from message: *");
    }

    [Fact]
    public void Given_CorruptQueryData_When_TheMessageIsParsed_Then_DeserializationFails()
    {
        var actor = _fixture.CreateQueryActor();
        var subject = new ActorSubject(
            ActorType.Query, FuturesBarDataQueryActor.ActorName,
            GetFuturesBarDataQuery.Verb, "ES.ES.2025-06-15");
        var message = new NatsMsg<byte[]> { Subject = subject.ToString(), Data = [0x00, 0x01, 0xFF] };

        Action act = () => actor.InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Given_NoQueryContext_When_TheMessageIsParsed_Then_ItIsRejected()
    {
        var actor = _fixture.CreateQueryActor();
        var query = CreateLastQuery(SampleData.FuturesBarData);

        Action act = () => actor.InvokeParseMessage(null!, CreateMessage(query));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(SampleData.FuturesBarDataCases), MemberType = typeof(SampleData))]
    public async Task Given_BarsExistForAOneMinuteRange_When_TheRangeQueryIsReceived_Then_TheDatabaseRowsAreReplied(
        FuturesBarDataReadModel barData, DateTime windowStart, DateTime windowEnd)
    {
        (windowEnd - windowStart).Should().Be(TimeSpan.FromMinutes(1));
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        var expected = new List<FuturesBarDataReadModel> { barData, SampleData.FuturesBarDataAlternate };
        db.GetFuturesBarDataAsync(barData.ContractId, barData.Symbol, barData.ValueDate, windowStart, windowEnd)
            .Returns(expected);
        var actor = _fixture.CreateQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery(barData, windowStart, windowEnd);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetFuturesBarDataAsync(
            barData.ContractId, barData.Symbol, barData.ValueDate, windowStart, windowEnd);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel[]>>(result =>
                result.Success && result.Value != null && result.Value.SequenceEqual(expected)));
    }

    [Theory]
    [MemberData(nameof(SampleData.FuturesBarDataCases), MemberType = typeof(SampleData))]
    public async Task Given_A_LastOneMinuteBarExists_When_TheLastQueryIsReceived_Then_ThatBarIsReplied(
        FuturesBarDataReadModel barData, DateTime windowStart, DateTime windowEnd)
    {
        (windowEnd - windowStart).Should().Be(TimeSpan.FromMinutes(1));
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetLastFuturesBarDataAsync(barData.ContractId, barData.Symbol, barData.ValueDate)
            .Returns(barData);
        var actor = _fixture.CreateQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery(barData);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetLastFuturesBarDataAsync(
            barData.ContractId, barData.Symbol, barData.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel>>(result =>
                result.Success && result.Value == barData));
    }

    [Fact]
    public async Task Given_NoBarsExist_When_AValidRangeQueryIsReceived_Then_AnEmptySuccessfulResultIsReplied()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetFuturesBarDataAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Array.Empty<FuturesBarDataReadModel>());
        var actor = _fixture.CreateQueryActor(dbFactory);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery(
            SampleData.FuturesBarData,
            SampleData.FuturesBarData.BarDate,
            SampleData.FuturesBarData.BarDate.AddMinutes(1));

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel[]>>(result =>
                result.Success && result.Value != null && result.Value.Length == 0));
    }

    [Fact]
    public async Task Given_TheDatabaseFails_When_AQueryIsReceived_Then_TheDatabaseFailurePropagates()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        db.GetLastFuturesBarDataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns<Task<FuturesBarDataReadModel>>(_ => throw new InvalidOperationException("database failed"));
        var actor = _fixture.CreateQueryActor(dbFactory);
        var query = CreateLastQuery(SampleData.FuturesBarDataAlternate);

        Func<Task> act = () => actor.InvokeReceiveAsync(Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database failed");
    }

    [Fact]
    public async Task Given_MissingReceiveInputs_When_AQueryIsReceived_Then_EachMissingInputIsRejected()
    {
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery(SampleData.FuturesBarData);

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, query).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask()))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Given_AnUnsupportedQuery_When_ItIsReceived_Then_ItIsRejected()
    {
        var actor = _fixture.CreateQueryActor();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(ActorType.Query, FuturesBarDataQueryActor.ActorName, "Unknown", "entity"));

        Func<Task> act = () => actor.InvokeReceiveAsync(Substitute.For<IQueryActorContext>(), query).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesBarDataQueryActor.ActorName} query: *");
    }

    [Fact]
    public async Task Given_A_RangeQueryFailure_When_TheExceptionIsHandled_Then_AFailedRangeResultIsReplied()
    {
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery(
            SampleData.FuturesBarDataAlternate,
            SampleData.FuturesBarDataAlternate.BarDate,
            SampleData.FuturesBarDataAlternate.BarDate.AddMinutes(1));

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, GetFuturesBarDataQuery.Verb,
            new TimeoutException("range timed out"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel[]>>(result =>
                !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == "range timed out"));
    }

    [Fact]
    public async Task Given_A_LastQueryFailure_When_TheExceptionIsHandled_Then_AFailedLastBarResultIsReplied()
    {
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery(SampleData.FuturesBarDataAlternate);

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, GetLastFuturesBarDataQuery.Verb,
            new InvalidOperationException("last lookup failed"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesBarDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesBarDataReadModel>>(result =>
                !result.Success && result.ErrorCode == query.ErrorCode && result.ErrorMessage == "last lookup failed"));
    }

    [Fact]
    public async Task Given_AnUnknownQueryFailure_When_TheExceptionIsHandled_Then_TheFallbackFailureIsReplied()
    {
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = Substitute.For<IQuery>();
        query.ErrorCode.Returns(9999);
        query.Subject.Returns(new ActorSubject(ActorType.Query, FuturesBarDataQueryActor.ActorName, "Unknown", "entity"));

        await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, "Unknown", new Exception("unknown failure"));

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            "Unknown",
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success && result.ErrorCode == 9999 && result.ErrorMessage == "unknown failure"));
    }

    [Fact]
    public async Task Given_ReplyingToAnExceptionAlsoFails_When_TheExceptionIsHandled_Then_TheSecondaryFailureIsSwallowed()
    {
        var actor = _fixture.CreateQueryActor(logger: Substitute.For<ILogger<FuturesBarDataQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateRangeQuery(
            SampleData.FuturesBarData,
            SampleData.FuturesBarData.BarDate,
            SampleData.FuturesBarData.BarDate.AddMinutes(1));
        context.ReplyAsync(
                Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ServiceResult<FuturesBarDataReadModel[]>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        Func<Task> act = () => actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, GetFuturesBarDataQuery.Verb,
            new Exception("original failure")).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_MissingExceptionInputs_When_AnExceptionIsHandled_Then_EachMissingInputIsRejected()
    {
        var actor = _fixture.CreateQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateLastQuery(SampleData.FuturesBarData);
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

    static GetFuturesBarDataQuery CreateRangeQuery(
        FuturesBarDataReadModel barData, DateTime startDate, DateTime endDate)
    {
        var entityId = new GetFuturesBarDataParameter(
            barData.ContractId, barData.Symbol, barData.ValueDate, startDate, endDate);
        return new GetFuturesBarDataQuery(
            barData.ContractId, barData.Symbol, barData.ValueDate, startDate, endDate)
        {
            Subject = new ActorSubject(
                ActorType.Query, FuturesBarDataQueryActor.ActorName,
                GetFuturesBarDataQuery.Verb, entityId.Format())
        };
    }

    static GetLastFuturesBarDataQuery CreateLastQuery(FuturesBarDataReadModel barData)
    {
        var entityId = new GetLastFuturesBarDataParameter(
            barData.ContractId, barData.Symbol, barData.ValueDate);
        return new GetLastFuturesBarDataQuery(barData.ContractId, barData.Symbol, barData.ValueDate)
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
