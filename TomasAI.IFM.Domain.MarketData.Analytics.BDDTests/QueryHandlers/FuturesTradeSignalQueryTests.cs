using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.QueryHandlers;

/// <summary>
/// BDD specifications for the complete <see cref="FuturesTradeSignalQueryActor"/> pipeline,
/// covering all query contracts, message routing, storage replies, and failures over daily,
/// weekly, and monthly Trade Signal data.
/// </summary>
public class FuturesTradeSignalQueryTests
{
    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTradeSignalQueryTests()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    sealed class TestableFuturesTradeSignalQueryActor(
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

    static Scenario CreateScenario()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var logger = Substitute.For<ILogger<FuturesTradeSignalQueryActor>>();
        var actor = new TestableFuturesTradeSignalQueryActor(dbFactory, logger);
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(true);
        return new Scenario(actor, dbFactory, db, logger, context);
    }

    static NatsMsg<byte[]> CreateMessage<TQuery>(
        TQuery query,
        byte[]? payload = null,
        string? subject = null)
        where TQuery : class, IQuery
        => new(
            subject ?? query.Subject.ToString(),
            string.Empty,
            0,
            default!,
            payload ?? ActorExtensions.DataSerializer.Serialize(query),
            default!,
            NatsMsgFlags.None);

    // Message parsing

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenATradeSignalQueryMessage_WhenItIsParsed_ThenTheContractQueryAndContextAreRestored(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.TradeSignalQueryFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<GetFuturesTradeSignalQuery>().Subject;
        parsed.ContractId.Should().Be(expected.ContractId);
        parsed.ValueDate.Should().Be(expected.ValueDate);
        parsed.Subject.Should().Be(expected.Subject);
        scenario.Context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesTradeSignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesTradeSignalQuery));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenALastTradeSignalQueryMessage_WhenItIsParsed_ThenTheQueryAndContextAreRestored(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.LastTradeSignalQueryFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        result.Should().BeOfType<GetLastFuturesTradeSignalQuery>()
            .Which.Subject.Should().Be(expected.Subject);
        scenario.Context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetLastFuturesTradeSignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetLastFuturesTradeSignalQuery));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenATradeSignalIdsQueryMessage_WhenItIsParsed_ThenTheDateQueryAndContextAreRestored(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var expected = SampleData.TradeSignalIdsQueryFor(timePeriod);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(expected));

        var parsed = result.Should().BeOfType<GetFuturesTradeSignalIdsQuery>().Subject;
        parsed.ValueDate.Should().Be(expected.ValueDate);
        parsed.Subject.Should().Be(expected.Subject);
        scenario.Context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesTradeSignalIdsQuery));
    }

    [Fact]
    public void GivenANullContext_WhenATradeSignalMessageIsParsed_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

        var act = () => scenario.Actor.InvokeParseMessage(null!, CreateMessage(query));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Command", "FuturesTradeSignalQuery", "GetFuturesTradeSignal")]
    [InlineData("Query", "WrongTradeSignalQueryActor", "GetFuturesTradeSignal")]
    [InlineData("Query", "FuturesTradeSignalQuery", "UnknownVerb")]
    public void GivenAnUnroutableSubject_WhenATradeSignalMessageIsParsed_ThenAResolutionErrorIsReported(
        string actorType,
        string actorName,
        string verb)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);
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
    public void GivenACorruptedPayload_WhenATradeSignalMessageIsParsed_ThenDeserializationFails(
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
    public void GivenAnEmptyPayload_WhenATradeSignalIdsMessageIsParsed_ThenDeserializationFails()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(TradeTimePeriodType.Monthly);

        var act = () => scenario.Actor.InvokeParseMessage(
            scenario.Context,
            CreateMessage(query, []));

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void GivenMessageContextRegistrationReturnsFalse_WhenAQueryIsParsed_ThenTheQueryIsStillReturned()
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(TradeTimePeriodType.Weekly);
        scenario.Context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(false);

        var result = scenario.Actor.InvokeParseMessage(scenario.Context, CreateMessage(query));

        result.Should().BeOfType<GetLastFuturesTradeSignalQuery>();
    }

    // Contract/date Trade Signal lookup

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnExistingTradeSignal_WhenTheContractQueryIsReceived_ThenThePeriodSignalIsReturned(
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
    public async Task GivenNoTradeSignal_WhenTheContractQueryIsReceived_ThenASuccessfulNullResultIsReturned(
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

    [Fact]
    public async Task GivenCustomContractAndDate_WhenTheContractQueryIsReceived_ThenBothAreForwardedToStorage()
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
            valueDate,
            direction: FuturesTrendDirectionType.DownTrending);
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
    public async Task GivenStorageFails_WhenTheContractQueryIsReceived_ThenTheFailureIsPropagated(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(timePeriod);
        scenario.Db.GetLastFuturesTradeSignalAsync(query.ContractId, query.ValueDate)
            .Returns<Task<FuturesTradeSignalV2ReadModel?>>(_ =>
                throw new InvalidOperationException($"{timePeriod} contract lookup unavailable"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{timePeriod} contract lookup unavailable");
        await scenario.Context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<FuturesTradeSignalV2ReadModel?>)!);
    }

    // Latest Trade Signal lookup

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnExistingLatestTradeSignal_WhenTheLastQueryIsReceived_ThenThePeriodSignalIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(timePeriod);
        var expected = SampleData.TradeSignalReadModelFor(timePeriod, sequenceId: 2);
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

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoLatestTradeSignal_WhenTheLastQueryIsReceived_ThenASuccessfulNullResultIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(timePeriod);
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
    public async Task GivenStorageFails_WhenTheLastQueryIsReceived_ThenTheFailureIsPropagated(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(timePeriod);
        scenario.Db.GetLastFuturesTradeSignalAsync()
            .Returns<Task<FuturesTradeSignalV2ReadModel?>>(_ =>
                throw new InvalidOperationException($"{timePeriod} latest lookup unavailable"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{timePeriod} latest lookup unavailable");
    }

    // Trade Signal identifier lookup

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenTradeSignalIds_WhenTheIdsQueryIsReceived_ThenAllPeriodIdsAreReturned(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(timePeriod);
        ICollection<FuturesTradeSignalId> expected =
        [
            SampleData.TradeSignalIdFor(timePeriod, 1),
            SampleData.TradeSignalIdFor(timePeriod, 2)
        ];
        scenario.Db.GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate)
            .Returns(Task.FromResult(expected));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Db.Received(1).GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate);
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalId[]>>(result =>
                result.Success
                && result.Value!.SequenceEqual(expected)
                && result.Value.All(id => id.TimePeriod == timePeriod)));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoTradeSignalIds_WhenTheIdsQueryIsReceived_ThenAnEmptySuccessfulResultIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(timePeriod);
        ICollection<FuturesTradeSignalId> noIds = [];
        scenario.Db.GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate)
            .Returns(Task.FromResult(noIds));

        await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Is<ServiceResult<FuturesTradeSignalId[]>>(result =>
                result.Success && result.Value!.Length == 0));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenStorageFails_WhenTheIdsQueryIsReceived_ThenTheFailureIsPropagated(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(timePeriod);
        scenario.Db.GetFuturesTradeSignalIdByValueDateAsync(query.ValueDate)
            .Returns<Task<ICollection<FuturesTradeSignalId>>>(_ =>
                throw new InvalidOperationException($"{timePeriod} id lookup unavailable"));

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{timePeriod} id lookup unavailable");
    }

    // Receive edge cases

    [Fact]
    public async Task GivenANullContext_WhenATradeSignalQueryIsReceived_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await scenario.Actor.InvokeReceiveAsync(null!, query);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullQuery_WhenItIsReceived_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenAnUnsupportedQuery_WhenItIsReceived_ThenAProcessingErrorIsReported()
    {
        var scenario = CreateScenario();
        var query = Substitute.For<IQuery>();

        var act = async () => await scenario.Actor.InvokeReceiveAsync(scenario.Context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesTradeSignalQueryActor.ActorName} query: *");
    }

    // Exception replies

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAContractQueryFailure_WhenTheActorHandlesIt_ThenATypedFailureIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} contract query failed");

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
    public async Task GivenALastQueryFailure_WhenTheActorHandlesIt_ThenATypedFailureIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.LastTradeSignalQueryFor(timePeriod);
        var exception = new TimeoutException($"{timePeriod} latest query failed");

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
    public async Task GivenAnIdsQueryFailure_WhenTheActorHandlesIt_ThenATypedArrayFailureIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} ids query failed");

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
    public async Task GivenAnUnsupportedQueryFailure_WhenTheActorHandlesIt_ThenAGenericFailureIsReturned()
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
    public async Task GivenInvalidFailureArguments_WhenTheActorHandlesThem_ThenArgumentNullIsReported()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalQueryFor(TradeTimePeriodType.Daily);

        var nullContext = async () => await scenario.Actor.InvokeOnExceptionAsync(
            null!, query.Subject.ThreadId, query, query.Subject.Verb, new Exception("failure"));
        var emptyThread = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context, default, query, query.Subject.Verb, new Exception("failure"));
        var nullQuery = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context, query.Subject.ThreadId, null!, query.Subject.Verb, new Exception("failure"));
        var nullVerb = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context, query.Subject.ThreadId, query, null!, new Exception("failure"));
        var nullException = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context, query.Subject.ThreadId, query, query.Subject.Verb, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await emptyThread.Should().ThrowAsync<ArgumentNullException>();
        await nullQuery.Should().ThrowAsync<ArgumentNullException>();
        await nullVerb.Should().ThrowAsync<ArgumentNullException>();
        await nullException.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenTheFailureReplyAlsoFails_WhenTheActorHandlesIt_ThenTheSecondaryFailureIsLoggedAndContained()
    {
        var scenario = CreateScenario();
        var query = SampleData.TradeSignalIdsQueryFor(TradeTimePeriodType.Monthly);
        scenario.Context.ReplyAsync(
                query.Subject.ThreadId,
                GetFuturesTradeSignalIdsQuery.Verb,
                Arg.Any<ServiceResult<FuturesTradeSignalId[]>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        var act = async () => await scenario.Actor.InvokeOnExceptionAsync(
            scenario.Context,
            query.Subject.ThreadId,
            query,
            GetFuturesTradeSignalIdsQuery.Verb,
            new InvalidOperationException("query failed"));

        await act.Should().NotThrowAsync();
        await scenario.Context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTradeSignalIdsQuery.Verb,
            Arg.Any<ServiceResult<FuturesTradeSignalId[]>>());
        scenario.Logger.ReceivedCalls()
            .Should().ContainSingle(call =>
                call.GetMethodInfo().Name == nameof(ILogger.Log)
                && (LogLevel)call.GetArguments()[0]! == LogLevel.Error);
    }
}
