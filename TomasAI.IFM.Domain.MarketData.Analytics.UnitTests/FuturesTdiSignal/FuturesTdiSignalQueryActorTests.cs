using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public class FuturesTdiSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTdiSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public sealed class TestableFuturesTdiSignalQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesTdiSignalQueryActor> logger)
        : FuturesTdiSignalQueryActor(dbFactory, logger)
    {
        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => ReceiveAsync(context, query);

        public ValueTask InvokeOnExceptionAsync(
            IQueryActorContext context,
            ActorThreadId threadId,
            IQuery query,
            string verb,
            Exception exception)
            => OnExceptionAsync(context, threadId, query, verb, exception);
    }

    sealed record Scenario(
        TestableFuturesTdiSignalQueryActor Actor,
        IDbContextFactory DbFactory,
        IMarketDataDbContext Db,
        ILogger<FuturesTdiSignalQueryActor> Logger,
        IQueryActorContext Context);

    Scenario CreateScenario()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var logger = Substitute.For<ILogger<FuturesTdiSignalQueryActor>>();
        var actor = _fixture.CreateActor(dbFactory, logger);
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(true);
        return new Scenario(actor, dbFactory, db, logger, context);
    }

    NatsMsg<byte[]> CreateMessage(
        GetFuturesTdiSignalQuery query,
        byte[]? payload = null,
        string? subject = null)
        => new(
            subject ?? query.Subject.ToString(),
            string.Empty,
            0,
            default!,
            payload ?? _fixture.DataSerializer.Serialize(query),
            default!,
            NatsMsgFlags.None);

    // ParseMessage

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_ValidQuery_DeserializesAllFieldsAndSetsMessageInfo(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.TdiQueryFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<GetFuturesTdiSignalQuery>().Subject;
        parsed.Should().BeEquivalentTo(expected);
        scenario.Context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesTdiSignalQuery));
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(null!, CreateMessage(query));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Command", "FuturesTdiSignalQuery", "GetFuturesTdiSignal")]
    [InlineData("Query", "WrongTdiQueryActor", "GetFuturesTdiSignal")]
    [InlineData("Query", "FuturesTdiSignalQuery", "UnknownVerb")]
    public void ParseMessage_UnroutableSubject_ThrowsInvalidOperationException(
        string actorType,
        string actorName,
        string verb)
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Daily);
        var subject = $"{actorType}.{actorName}.{verb}.{query.Subject.ThreadId.EntityId}";

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(query, subject: subject));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalQueryActor.ActorName} query from message: {subject}");
        scenario.Context.DidNotReceiveWithAnyArgs().SetMessageInfo(default, default!, default!);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_CorruptedPayload_ThrowsDeserializationException(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(timePeriod);
        byte[] corruptedPayload = [0x00, 0x01, 0x02, 0xff, 0xfe];

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(query, corruptedPayload));

        act.Should().Throw<Exception>();
        scenario.Context.DidNotReceiveWithAnyArgs().SetMessageInfo(default, default!, default!);
    }

    [Fact]
    public void ParseMessage_EmptyPayload_ThrowsDeserializationException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Monthly);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(query, []));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseMessage_EmptyEntityId_ParsesQueryAndSetsMessageInfo()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Weekly) with
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetFuturesTdiSignalQuery.Actor,
                GetFuturesTdiSignalQuery.Verb,
                string.Empty)
        };

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(query));

        result.Should().BeOfType<GetFuturesTdiSignalQuery>();
        scenario.Context.Received(1).SetMessageInfo(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Any<ActorMessageInfo>());
    }

    // ReceiveAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_ExistingSignal_QueriesStorageAndRepliesWithSignal(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(timePeriod);
        var expected = SampleData.TdiReadModelFor(timePeriod);
        scenario.Db.GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate)
            .Returns(Task.FromResult<FuturesTdiSignalReadModel?>(expected));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Db.Received(1).GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate);
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result =>
                result.Success
                && result.Value == expected
                && result.Value.TimePeriod == timePeriod));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_MissingSignal_RepliesWithSuccessfulNullResult(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(timePeriod);
        scenario.Db.GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate)
            .Returns(Task.FromResult<FuturesTdiSignalReadModel?>(null));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    [Fact]
    public async Task ReceiveAsync_CustomContractAndDate_ForwardsBothDimensionsToStorage()
    {
        const string contractId = "NQZ27";
        var valueDate = new DateOnly(2027, 12, 17);
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(
            TradeTimePeriodType.Weekly,
            contractId,
            valueDate);
        var expected = SampleData.TdiReadModelFor(
            TradeTimePeriodType.Weekly,
            contractId,
            valueDate,
            direction: FuturesTrendDirectionType.DownTrending);
        scenario.Db.GetLastFuturesTdiSignalAsync(contractId, valueDate)
            .Returns(Task.FromResult<FuturesTdiSignalReadModel?>(expected));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Db.Received(1).GetLastFuturesTdiSignalAsync(contractId, valueDate);
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result => result.Value == expected));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_StorageFailure_PropagatesExceptionWithoutReply(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(timePeriod);
        scenario.Db.GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate)
            .Returns<Task<FuturesTdiSignalReadModel?>>(_ =>
                throw new InvalidOperationException($"{timePeriod} storage unavailable"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{timePeriod} storage unavailable");
        await scenario.Context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<FuturesTdiSignalReadModel?>)!);
    }

    [Fact]
    public async Task ReceiveAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(null!, query);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_NullQuery_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReceiveAsync_UnsupportedQuery_ThrowsInvalidOperationException()
    {
        var scenario = CreateScenario();
        var query = Substitute.For<IQuery>();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesTdiSignalQueryActor.ActorName} query: *");
    }

    // OnExceptionAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_TdiQuery_RepliesWithTypedFailure(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} lookup failed");

        await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            GetFuturesTdiSignalQuery.Verb,
            exception);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result =>
                !result.Success
                && result.ErrorCode == query.ErrorCode
                && result.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task OnExceptionAsync_UnsupportedQuery_RepliesWithGenericFailure()
    {
        var scenario = CreateScenario();
        var query = Substitute.For<IQuery>();
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, "unsupported");
        var exception = new InvalidOperationException("unsupported query failed");

        await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            threadId,
            query,
            "UnknownVerb",
            exception);

        await scenario.Context.Received(1).ReplyAsync(
            threadId,
            "UnknownVerb",
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success
                && result.ErrorCode == 9999
                && result.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task OnExceptionAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            null!,
            query.Subject.ThreadId,
            query,
            query.Subject.Verb,
            new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_EmptyThreadId_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            default,
            query,
            query.Subject.Verb,
            new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_NullQuery_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, "null-query");

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            threadId,
            null!,
            GetFuturesTdiSignalQuery.Verb,
            new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_NullVerb_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            null!,
            new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_NullException_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            query.Subject.Verb,
            null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ReplyFailure_IsCaughtAndContained()
    {
        var scenario = CreateScenario();
        var query = SampleData.TdiQueryFor(TradeTimePeriodType.Monthly);
        scenario.Context.ReplyAsync(
                query.Subject.ThreadId,
                GetFuturesTdiSignalQuery.Verb,
                Arg.Any<ServiceResult<FuturesTdiSignalReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            GetFuturesTdiSignalQuery.Verb,
            new InvalidOperationException("query failed"));

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Any<ServiceResult<FuturesTdiSignalReadModel?>>());
    }
}
