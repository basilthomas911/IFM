using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal;

public class FuturesTradeSignalQueryActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTradeSignalQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public sealed class TestableFuturesTradeSignalQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesTradeSignalQueryActor> logger)
        : FuturesTradeSignalQueryActor(dbFactory, logger)
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
        TestableFuturesTradeSignalQueryActor Actor,
        IDbContextFactory DbFactory,
        IMarketDataDbContext Db,
        ILogger<FuturesTradeSignalQueryActor> Logger,
        IQueryActorContext Context);

    Scenario CreateScenario()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var logger = Substitute.For<ILogger<FuturesTradeSignalQueryActor>>();
        var actor = _fixture.CreateActor(dbFactory, logger);
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(true);
        return new Scenario(actor, dbFactory, db, logger, context);
    }

    NatsMsg<byte[]> CreateMessage<TQuery>(
        TQuery query,
        byte[]? payload = null,
        string? subject = null)
        where TQuery : class, IQuery
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
    public void ParseMessage_GetSignalQuery_DeserializesAndSetsMessageInfo(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.TradeSignalQueryFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<GetFuturesTradeSignalQuery>().Subject;
        parsed.Should().BeEquivalentTo(expected);
        scenario.Context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesTradeSignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesTradeSignalQuery));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_GetLastSignalQuery_DeserializesAndSetsMessageInfo(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.LastTradeSignalQueryFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<GetLastFuturesTradeSignalQuery>().Subject;
        parsed.Subject.Should().Be(expected.Subject);
        scenario.Context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetLastFuturesTradeSignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetLastFuturesTradeSignalQuery));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_GetSignalIdsQuery_DeserializesAndSetsMessageInfo(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.TradeSignalIdsQueryFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<GetFuturesTradeSignalIdsQuery>().Subject;
        parsed.Should().BeEquivalentTo(expected);
        scenario.Context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesTradeSignalIdsQuery));
    }

    [Fact]
    public void ParseMessage_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(null!, CreateMessage(query));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Command", FuturesTradeSignalQueryActor.ActorName, GetFuturesTradeSignalQuery.Verb)]
    [InlineData("Query", "WrongTradeSignalQueryActor", GetFuturesTradeSignalQuery.Verb)]
    [InlineData("Query", FuturesTradeSignalQueryActor.ActorName, "UnknownVerb")]
    public void ParseMessage_UnroutableSubject_ThrowsInvalidOperationException(
        string actorType,
        string actorName,
        string verb)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Weekly);
        var subject = $"{actorType}.{actorName}.{verb}.{query.Subject.ThreadId.EntityId}";

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(query, subject: subject));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTradeSignalQueryActor.ActorName} query from message: {subject}");
        scenario.Context.DidNotReceiveWithAnyArgs().SetMessageInfo(default, default!, default!);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void ParseMessage_CorruptedPayload_ThrowsDeserializationException(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(timePeriod);
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
        var query = SampleData.TradeSignalIdsQueryFor(TradeTimePeriodType.Monthly);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(query, []));

        act.Should().Throw<Exception>();
    }

    // ReceiveAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_GetSignalQuery_ExistingSignalRepliesWithPeriodSpecificResult(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(timePeriod);
        var expected = SampleData.TradeSignalReadModelFor(timePeriod);
        scenario.Db.GetLastFuturesTradeSignalAsync(query.ContractId, query.ValueDate)
            .Returns(Task.FromResult<FuturesTradeSignalV2ReadModel?>(expected));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Db.Received(1).GetLastFuturesTradeSignalAsync(query.ContractId, query.ValueDate);
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalV2ReadModel?>>(result =>
                result.Success
                && result.Value == expected
                && result.Value.TimePeriod == timePeriod));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_GetSignalQuery_MissingSignalRepliesWithSuccessfulNull(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(timePeriod);
        scenario.Db.GetLastFuturesTradeSignalAsync(query.ContractId, query.ValueDate)
            .Returns(Task.FromResult<FuturesTradeSignalV2ReadModel?>(null));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalV2ReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_GetLastSignalQuery_RepliesWithPeriodSpecificResult(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(timePeriod);
        var expected = SampleData.TradeSignalReadModelFor(timePeriod);
        scenario.Db.GetLastFuturesTradeSignalAsync()
            .Returns(Task.FromResult<FuturesTradeSignalV2ReadModel?>(expected));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Db.Received(1).GetLastFuturesTradeSignalAsync();
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesTradeSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalV2ReadModel?>>(result =>
                result.Success
                && result.Value == expected
                && result.Value.TimePeriod == timePeriod));
    }

    [Fact]
    public async Task ReceiveAsync_GetLastSignalQuery_MissingSignalRepliesWithSuccessfulNull()
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(TradeTimePeriodType.Daily);
        scenario.Db.GetLastFuturesTradeSignalAsync()
            .Returns(Task.FromResult<FuturesTradeSignalV2ReadModel?>(null));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesTradeSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalV2ReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_GetSignalIdsQuery_RepliesWithPeriodSpecificIds(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(timePeriod);
        FuturesTradeSignalId[] expected =
        [
            SampleData.TradeSignalIdFor(timePeriod, 1),
            SampleData.TradeSignalIdFor(timePeriod, 2)
        ];
        scenario.Db.GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate)
            .Returns(Task.FromResult<ICollection<FuturesTradeSignalId>>(expected));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Db.Received(1).GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate);
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalId[]>>(result =>
                result.Success
                && result.Value != null
                && result.Value.SequenceEqual(expected)
                && result.Value.All(id => id.TimePeriod == timePeriod)));
    }

    [Fact]
    public async Task ReceiveAsync_GetSignalIdsQuery_EmptyStorageResultRepliesWithEmptyArray()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(TradeTimePeriodType.Monthly);
        scenario.Db.GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate)
            .Returns(Task.FromResult<ICollection<FuturesTradeSignalId>>([]));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalId[]>>(result =>
                result.Success && result.Value != null && result.Value.Length == 0));
    }

    [Fact]
    public async Task ReceiveAsync_GetSignalQuery_ForwardsCustomContractAndDateToStorage()
    {
        const string contractId = "NQZ27";
        var valueDate = new DateOnly(2027, 12, 17);
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(
            TradeTimePeriodType.Weekly,
            contractId,
            valueDate);
        var expected = SampleData.TradeSignalReadModelFor(
            TradeTimePeriodType.Weekly,
            contractId,
            valueDate);
        scenario.Db.GetLastFuturesTradeSignalAsync(contractId, valueDate)
            .Returns(Task.FromResult<FuturesTradeSignalV2ReadModel?>(expected));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Db.Received(1).GetLastFuturesTradeSignalAsync(contractId, valueDate);
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalV2ReadModel?>>(result => result.Value == expected));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task ReceiveAsync_GetSignalQuery_StorageFailurePropagatesWithoutReply(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(timePeriod);
        scenario.Db.GetLastFuturesTradeSignalAsync(query.ContractId, query.ValueDate)
            .Returns<Task<FuturesTradeSignalV2ReadModel?>>(_ =>
                throw new InvalidOperationException($"{timePeriod} storage unavailable"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{timePeriod} storage unavailable");
        await scenario.Context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<FuturesTradeSignalV2ReadModel?>)!);
    }

    [Fact]
    public async Task ReceiveAsync_GetLastSignalQuery_StorageFailurePropagates()
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(TradeTimePeriodType.Weekly);
        scenario.Db.GetLastFuturesTradeSignalAsync()
            .Returns<Task<FuturesTradeSignalV2ReadModel?>>(_ =>
                throw new TimeoutException("latest signal timed out"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("latest signal timed out");
    }

    [Fact]
    public async Task ReceiveAsync_GetSignalIdsQuery_StorageFailurePropagates()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(TradeTimePeriodType.Monthly);
        scenario.Db.GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate)
            .Returns<Task<ICollection<FuturesTradeSignalId>>>(_ =>
                throw new InvalidOperationException("ids unavailable"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ids unavailable");
    }

    [Fact]
    public async Task ReceiveAsync_ReplyFailurePropagates()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);
        var expected = SampleData.TradeSignalReadModelFor(TradeTimePeriodType.Daily);
        scenario.Db.GetLastFuturesTradeSignalAsync(query.ContractId, query.ValueDate)
            .Returns(Task.FromResult<FuturesTradeSignalV2ReadModel?>(expected));
        scenario.Context.ReplyAsync(
                query.Subject.ThreadId,
                GetFuturesTradeSignalQuery.Verb,
                Arg.Any<ServiceResult<FuturesTradeSignalV2ReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("reply failed");
    }

    [Fact]
    public async Task ReceiveAsync_NullContext_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

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
            .WithMessage($"Unable to process {FuturesTradeSignalQueryActor.ActorName} query: *");
    }

    // OnExceptionAsync

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_GetSignalQuery_RepliesWithTypedFailure(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} signal lookup failed");

        await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            GetFuturesTradeSignalQuery.Verb,
            exception);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalV2ReadModel?>>(result =>
                !result.Success
                && result.ErrorCode == query.ErrorCode
                && result.ErrorMessage == exception.Message));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_GetLastSignalQuery_RepliesWithTypedFailure(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(timePeriod);
        var exception = new TimeoutException($"{timePeriod} latest lookup timed out");

        await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            GetLastFuturesTradeSignalQuery.Verb,
            exception);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetLastFuturesTradeSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalV2ReadModel?>>(result =>
                !result.Success
                && result.ErrorCode == query.ErrorCode
                && result.ErrorMessage == exception.Message));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task OnExceptionAsync_GetSignalIdsQuery_RepliesWithTypedFailure(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} ids lookup failed");

        await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            GetFuturesTradeSignalIdsQuery.Verb,
            exception);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalId[]>>(result =>
                !result.Success
                && result.ErrorCode == query.ErrorCode
                && result.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task OnExceptionAsync_UnsupportedQuery_RepliesWithGenericFailure()
    {
        var scenario = CreateScenario();
        var query = Substitute.For<IQuery>();
        var threadId = new ActorThreadId(
            ActorType.Query,
            FuturesTradeSignalQueryActor.ActorName,
            "unsupported");
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
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

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
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

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
        var threadId = new ActorThreadId(
            ActorType.Query,
            FuturesTradeSignalQueryActor.ActorName,
            "null-query");

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            threadId,
            null!,
            GetFuturesTradeSignalQuery.Verb,
            new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_NullVerb_ThrowsArgumentNullException()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

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
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

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
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Monthly);
        scenario.Context.ReplyAsync(
                query.Subject.ThreadId,
                GetFuturesTradeSignalQuery.Verb,
                Arg.Any<ServiceResult<FuturesTradeSignalV2ReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            GetFuturesTradeSignalQuery.Verb,
            new InvalidOperationException("query failed"));

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalQuery.Verb,
            Arg.Any<ServiceResult<FuturesTradeSignalV2ReadModel?>>());
    }
}
