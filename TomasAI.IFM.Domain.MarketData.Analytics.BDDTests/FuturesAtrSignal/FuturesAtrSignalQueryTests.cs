using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query.Actor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesAtrSignal;

/// <summary>
/// BDD-style specifications for the complete <see cref="FuturesAtrSignalQueryActor"/> query pipeline.
/// The scenarios cover message parsing, query dispatch, database responses, and failure replies for
/// daily, weekly, and monthly ATR signals.
/// </summary>
public class FuturesAtrSignalQueryTests
{
    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesAtrSignalQueryTests()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    sealed class TestableFuturesAtrSignalQueryActor(
        IDbContextFactory dbFactory,
        ILogger<FuturesAtrSignalQueryActor> logger)
        : FuturesAtrSignalQueryActor(dbFactory, logger)
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

    static (TestableFuturesAtrSignalQueryActor Actor, IMarketDataDbContext Db, IQueryActorContext Context)
        CreateScenario()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = new TestableFuturesAtrSignalQueryActor(
            dbFactory,
            Substitute.For<ILogger<FuturesAtrSignalQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(true);
        return (actor, db, context);
    }

    static NatsMsg<byte[]> CreateMessage<TQuery>(TQuery query, byte[]? payload = null, string? subject = null)
        where TQuery : class, IQuery
        => new(
            subject ?? query.Subject.ToString(),
            string.Empty,
            0,
            default!,
            payload ?? ActorExtensions.DataSerializer.Serialize(query),
            default!,
            NatsMsgFlags.None);

    // Message parsing: happy paths

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenAnAtrSignalMessage_WhenItIsParsed_ThenTheQueryAndMessageContextAreRestored(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var expected = SampleData.AtrSignalQueryFor(timePeriod);

        var result = actor.InvokeParseMessage(context, CreateMessage(expected));

        result.Should().BeEquivalentTo(expected);
        context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesAtrSignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesAtrSignalQuery));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenAnAtrDailySignalMessage_WhenItIsParsed_ThenTheQueryAndMessageContextAreRestored(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var expected = SampleData.AtrDailySignalQueryFor(timePeriod);

        var result = actor.InvokeParseMessage(context, CreateMessage(expected));

        result.Should().BeEquivalentTo(expected);
        context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesAtrDailySignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesAtrDailySignalQuery));
    }

    // Message parsing: edge cases

    [Fact]
    public void GivenANullContext_WhenAnAtrMessageIsParsed_ThenArgumentNullIsReported()
    {
        var (actor, _, _) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);

        var act = () => actor.InvokeParseMessage(null!, CreateMessage(query));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Command", "FuturesAtrSignalQuery", "GetFuturesAtrSignal")]
    [InlineData("Query", "WrongAtrActor", "GetFuturesAtrSignal")]
    [InlineData("Query", "FuturesAtrSignalQuery", "UnknownVerb")]
    public void GivenAnUnroutableSubject_WhenAnAtrMessageIsParsed_ThenAResolutionErrorIsReported(
        string actorType,
        string actorName,
        string verb)
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);
        var subject = $"{actorType}.{actorName}.{verb}.{query.Subject.ThreadId.EntityId}";

        var act = () => actor.InvokeParseMessage(context, CreateMessage(query, subject: subject));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesAtrSignalQueryActor.ActorName} query from message: {subject}");
        context.DidNotReceiveWithAnyArgs().SetMessageInfo(default, default!, default!);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenACorruptedPayload_WhenAnAtrMessageIsParsed_ThenDeserializationFails(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(timePeriod);
        byte[] corruptedPayload = [0x00, 0x01, 0x02, 0xff, 0xfe];

        var act = () => actor.InvokeParseMessage(context, CreateMessage(query, corruptedPayload));

        act.Should().Throw<Exception>();
        context.DidNotReceiveWithAnyArgs().SetMessageInfo(default, default!, default!);
    }

    // Current ATR signal query: happy paths and boundaries

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnExistingAtrSignal_WhenTheQueryIsReceived_ThenTheMatchingSignalIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(timePeriod);
        var expected = SampleData.AtrReadModelFor(timePeriod);
        db.GetLastFuturesAtrSignalAsync(query.ContractId, query.ValueDate, timePeriod, query.PeriodLength)
            .Returns(Task.FromResult<FuturesAtrSignalReadModel?>(expected));

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetLastFuturesAtrSignalAsync(
            query.ContractId,
            query.ValueDate,
            timePeriod,
            query.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(result =>
                result.Success && result.Value == expected));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoCurrentAtrSignal_WhenTheQueryIsReceived_ThenASuccessfulNullResultIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(timePeriod);
        db.GetLastFuturesAtrSignalAsync(query.ContractId, query.ValueDate, timePeriod, query.PeriodLength)
            .Returns(Task.FromResult<FuturesAtrSignalReadModel?>(null));

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    [Fact]
    public async Task GivenCustomAtrQueryDimensions_WhenTheQueryIsReceived_ThenEveryDimensionIsForwardedToStorage()
    {
        const string contractId = "NQZ26";
        const int periodLength = 21;
        var valueDate = new DateOnly(2026, 12, 18);
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(
            TradeTimePeriodType.Weekly,
            contractId,
            valueDate,
            periodLength);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetLastFuturesAtrSignalAsync(
            contractId,
            valueDate,
            TradeTimePeriodType.Weekly,
            periodLength);
    }

    // Daily ATR signal query: happy paths and boundaries

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnExistingDailyAtrSignal_WhenTheQueryIsReceived_ThenTheMatchingSignalIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrDailySignalQueryFor(timePeriod);
        var expected = SampleData.AtrReadModelFor(timePeriod);
        db.GetLastFuturesAtrDailySignalAsync(query.ContractId, timePeriod, query.PeriodLength)
            .Returns(Task.FromResult<FuturesAtrSignalReadModel?>(expected));

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetLastFuturesAtrDailySignalAsync(
            query.ContractId,
            timePeriod,
            query.PeriodLength);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrDailySignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(result =>
                result.Success && result.Value == expected));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoDailyAtrSignal_WhenTheQueryIsReceived_ThenASuccessfulNullResultIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrDailySignalQueryFor(timePeriod);
        db.GetLastFuturesAtrDailySignalAsync(query.ContractId, timePeriod, query.PeriodLength)
            .Returns(Task.FromResult<FuturesAtrSignalReadModel?>(null));

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrDailySignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    [Fact]
    public async Task GivenCustomDailyAtrDimensions_WhenTheQueryIsReceived_ThenEveryDimensionIsForwardedToStorage()
    {
        const string contractId = "CLF27";
        const int periodLength = 7;
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrDailySignalQueryFor(
            TradeTimePeriodType.Monthly,
            contractId,
            periodLength);

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetLastFuturesAtrDailySignalAsync(
            contractId,
            TradeTimePeriodType.Monthly,
            periodLength);
    }

    // Query dispatch failures

    [Fact]
    public async Task GivenANullContext_WhenAQueryIsReceived_ThenArgumentNullIsReported()
    {
        var (actor, _, _) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeReceiveAsync(null!, query);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullQuery_WhenItIsReceived_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();

        var act = async () => await actor.InvokeReceiveAsync(context, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenAnUnsupportedQuery_WhenItIsReceived_ThenAProcessingErrorIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var query = Substitute.For<IQuery>();

        var act = async () => await actor.InvokeReceiveAsync(context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to process {FuturesAtrSignalQueryActor.ActorName} query: *");
    }

    [Fact]
    public async Task GivenStorageFails_WhenTheCurrentAtrQueryIsReceived_ThenTheFailureIsPropagated()
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Weekly);
        db.GetLastFuturesAtrSignalAsync(query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength)
            .Returns<Task<FuturesAtrSignalReadModel?>>(_ => throw new InvalidOperationException("storage unavailable"));

        var act = async () => await actor.InvokeReceiveAsync(context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("storage unavailable");
        await context.DidNotReceiveWithAnyArgs().ReplyAsync(default, default!, default(ServiceResult<FuturesAtrSignalReadModel?>)!);
    }

    [Fact]
    public async Task GivenStorageFails_WhenTheDailyAtrQueryIsReceived_ThenTheFailureIsPropagated()
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.AtrDailySignalQueryFor(TradeTimePeriodType.Monthly);
        db.GetLastFuturesAtrDailySignalAsync(query.ContractId, query.TimePeriod, query.PeriodLength)
            .Returns<Task<FuturesAtrSignalReadModel?>>(_ => throw new InvalidOperationException("daily storage unavailable"));

        var act = async () => await actor.InvokeReceiveAsync(context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("daily storage unavailable");
        await context.DidNotReceiveWithAnyArgs().ReplyAsync(default, default!, default(ServiceResult<FuturesAtrSignalReadModel?>)!);
    }

    // Exception replies

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenACurrentAtrQueryFailure_WhenTheActorHandlesIt_ThenATypedFailureIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} ATR lookup failed");

        await actor.InvokeOnExceptionAsync(
            context,
            query.Subject.ThreadId,
            query,
            GetFuturesAtrSignalQuery.Verb,
            exception);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesAtrSignalReadModel?>>(result =>
                !result.Success
                && result.ErrorCode == query.ErrorCode
                && result.ErrorMessage == exception.Message));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenADailyAtrQueryFailure_WhenTheActorHandlesIt_ThenAGenericFailureIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrDailySignalQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} daily ATR lookup failed");

        await actor.InvokeOnExceptionAsync(
            context,
            query.Subject.ThreadId,
            query,
            GetFuturesAtrDailySignalQuery.Verb,
            exception);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesAtrDailySignalQuery.Verb,
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success
                && result.ErrorCode == 9999
                && result.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task GivenAnUnsupportedQueryFailure_WhenTheActorHandlesIt_ThenAGenericFailureIsReturned()
    {
        var (actor, _, context) = CreateScenario();
        var query = Substitute.For<IQuery>();
        var threadId = new ActorThreadId(ActorType.Query, FuturesAtrSignalQueryActor.ActorName, "unsupported");
        var exception = new InvalidOperationException("unsupported query failed");

        await actor.InvokeOnExceptionAsync(context, threadId, query, "UnknownVerb", exception);

        await context.Received(1).ReplyAsync(
            threadId,
            "UnknownVerb",
            Arg.Is<ServiceFailed<ActorEntityId>>(result =>
                !result.Success
                && result.ErrorCode == 9999
                && result.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task GivenANullContext_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, _) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            null!, query.Subject.ThreadId, query, query.Subject.Verb, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenAnEmptyThreadId_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, default, query, query.Subject.Verb, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullQuery_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Query, FuturesAtrSignalQueryActor.ActorName, "null-query");

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, threadId, null!, GetFuturesAtrSignalQuery.Verb, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullVerb_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, null!, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullException_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, query.Subject.Verb, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenTheFailureReplyAlsoFails_WhenTheActorHandlesIt_ThenTheSecondaryFailureIsContained()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.AtrSignalQueryFor(TradeTimePeriodType.Daily);
        context.ReplyAsync(
                query.Subject.ThreadId,
                GetFuturesAtrSignalQuery.Verb,
                Arg.Any<ServiceResult<FuturesAtrSignalReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        var act = async () => await actor.InvokeOnExceptionAsync(
            context,
            query.Subject.ThreadId,
            query,
            GetFuturesAtrSignalQuery.Verb,
            new InvalidOperationException("query failed"));

        await act.Should().NotThrowAsync();
    }
}
