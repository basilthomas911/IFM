using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query.Actor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesTdiSignal;

/// <summary>
/// BDD specifications for the complete <see cref="FuturesTdiSignalQueryActor"/> pipeline,
/// covering message routing, storage lookup, replies, and failure behavior for daily, weekly,
/// and monthly TDI signals.
/// </summary>
public class FuturesTdiSignalQueryTests
{
    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public FuturesTdiSignalQueryTests()
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        ActorExtensions.MsgSerializer ??= new NatsByteArrayMessageSerializer();
    }

    sealed class TestableFuturesTdiSignalQueryActor(
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

    static (TestableFuturesTdiSignalQueryActor Actor, IMarketDataDbContext Db, IQueryActorContext Context)
        CreateScenario()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(db);
        var actor = new TestableFuturesTdiSignalQueryActor(
            dbFactory,
            Substitute.For<ILogger<FuturesTdiSignalQueryActor>>());
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(true);
        return (actor, db, context);
    }

    static NatsMsg<byte[]> CreateMessage(
        GetFuturesTdiSignalQuery query,
        byte[]? payload = null,
        string? subject = null)
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
    public void GivenATdiQueryMessage_WhenItIsParsed_ThenTheQueryAndMessageContextAreRestored(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var expected = SampleData.TdiSignalQueryFor(timePeriod);

        var result = actor.InvokeParseMessage(context, CreateMessage(expected));

        result.Should().BeEquivalentTo(expected);
        context.Received(1).SetMessageInfo(
            expected.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ActorMessageInfo>(info => info.Query is GetFuturesTdiSignalQuery));
    }

    [Fact]
    public void GivenANullContext_WhenATdiMessageIsParsed_ThenArgumentNullIsReported()
    {
        var (actor, _, _) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);

        var act = () => actor.InvokeParseMessage(null!, CreateMessage(query));

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Command", "FuturesTdiSignalQuery", "GetFuturesTdiSignal")]
    [InlineData("Query", "WrongTdiQueryActor", "GetFuturesTdiSignal")]
    [InlineData("Query", "FuturesTdiSignalQuery", "UnknownVerb")]
    public void GivenAnUnroutableSubject_WhenATdiMessageIsParsed_ThenAResolutionErrorIsReported(
        string actorType,
        string actorName,
        string verb)
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);
        var subject = $"{actorType}.{actorName}.{verb}.{query.Subject.ThreadId.EntityId}";

        var act = () => actor.InvokeParseMessage(context, CreateMessage(query, subject: subject));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Unable to resolve {FuturesTdiSignalQueryActor.ActorName} query from message: {subject}");
        context.DidNotReceiveWithAnyArgs().SetMessageInfo(default, default!, default!);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenACorruptedPayload_WhenATdiMessageIsParsed_ThenDeserializationFails(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(timePeriod);
        byte[] corruptedPayload = [0x00, 0x01, 0x02, 0xff, 0xfe];

        var act = () => actor.InvokeParseMessage(context, CreateMessage(query, corruptedPayload));

        act.Should().Throw<Exception>();
        context.DidNotReceiveWithAnyArgs().SetMessageInfo(default, default!, default!);
    }

    [Fact]
    public void GivenAnEmptyPayload_WhenATdiMessageIsParsed_ThenDeserializationFails()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Monthly);

        var act = () => actor.InvokeParseMessage(context, CreateMessage(query, []));

        act.Should().Throw<Exception>();
    }

    // Storage query and replies

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenAnExistingTdiSignal_WhenTheQueryIsReceived_ThenTheMatchingSignalIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(timePeriod);
        var expected = SampleData.TdiReadModelFor(timePeriod);
        db.GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate)
            .Returns(Task.FromResult<FuturesTdiSignalReadModel?>(expected));

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result =>
                result.Success
                && result.Value == expected
                && result.Value.TimePeriod == timePeriod));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenNoTdiSignal_WhenTheQueryIsReceived_ThenASuccessfulNullResultIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(timePeriod);
        db.GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate)
            .Returns(Task.FromResult<FuturesTdiSignalReadModel?>(null));

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result =>
                result.Success && result.Value == null));
    }

    [Fact]
    public async Task GivenCustomQueryDimensions_WhenTheQueryIsReceived_ThenContractAndDateAreForwardedToStorage()
    {
        const string contractId = "NQZ27";
        var valueDate = new DateOnly(2027, 12, 17);
        var (actor, db, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(
            TradeTimePeriodType.Weekly,
            contractId,
            valueDate);
        var expected = SampleData.TdiReadModelFor(
            TradeTimePeriodType.Weekly,
            contractId,
            valueDate,
            direction: FuturesTrendDirectionType.DownTrending);
        db.GetLastFuturesTdiSignalAsync(contractId, valueDate)
            .Returns(Task.FromResult<FuturesTdiSignalReadModel?>(expected));

        await actor.InvokeReceiveAsync(context, query);

        await db.Received(1).GetLastFuturesTdiSignalAsync(contractId, valueDate);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result => result.Value == expected));
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenStorageFails_WhenTheTdiQueryIsReceived_ThenTheFailureIsPropagated(
        TradeTimePeriodType timePeriod)
    {
        var (actor, db, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(timePeriod);
        db.GetLastFuturesTdiSignalAsync(query.ContractId, query.ValueDate)
            .Returns<Task<FuturesTdiSignalReadModel?>>(_ =>
                throw new InvalidOperationException($"{timePeriod} storage unavailable"));

        var act = async () => await actor.InvokeReceiveAsync(context, query);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{timePeriod} storage unavailable");
        await context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<FuturesTdiSignalReadModel?>)!);
    }

    [Fact]
    public async Task GivenANullContext_WhenATdiQueryIsReceived_ThenArgumentNullIsReported()
    {
        var (actor, _, _) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);

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
            .WithMessage($"Unable to process {FuturesTdiSignalQueryActor.ActorName} query: *");
    }

    // Exception replies

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GivenATdiQueryFailure_WhenTheActorHandlesIt_ThenATypedFailureIsReturned(
        TradeTimePeriodType timePeriod)
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(timePeriod);
        var exception = new InvalidOperationException($"{timePeriod} TDI lookup failed");

        await actor.InvokeOnExceptionAsync(
            context,
            query.Subject.ThreadId,
            query,
            GetFuturesTdiSignalQuery.Verb,
            exception);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesTdiSignalReadModel?>>(result =>
                !result.Success
                && result.ErrorCode == query.ErrorCode
                && result.ErrorMessage == exception.Message));
    }

    [Fact]
    public async Task GivenAnUnsupportedQueryFailure_WhenTheActorHandlesIt_ThenAGenericFailureIsReturned()
    {
        var (actor, _, context) = CreateScenario();
        var query = Substitute.For<IQuery>();
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, "unsupported");
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
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            null!, query.Subject.ThreadId, query, query.Subject.Verb, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenAnEmptyThreadId_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, default, query, query.Subject.Verb, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullQuery_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var threadId = new ActorThreadId(ActorType.Query, FuturesTdiSignalQueryActor.ActorName, "null-query");

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, threadId, null!, GetFuturesTdiSignalQuery.Verb, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullVerb_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, null!, new Exception("failure"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenANullException_WhenAFailureIsHandled_ThenArgumentNullIsReported()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);

        var act = async () => await actor.InvokeOnExceptionAsync(
            context, query.Subject.ThreadId, query, query.Subject.Verb, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GivenTheFailureReplyAlsoFails_WhenTheActorHandlesIt_ThenTheSecondaryFailureIsContained()
    {
        var (actor, _, context) = CreateScenario();
        var query = SampleData.TdiSignalQueryFor(TradeTimePeriodType.Daily);
        context.ReplyAsync(
                query.Subject.ThreadId,
                GetFuturesTdiSignalQuery.Verb,
                Arg.Any<ServiceResult<FuturesTdiSignalReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("reply failed"));

        var act = async () => await actor.InvokeOnExceptionAsync(
            context,
            query.Subject.ThreadId,
            query,
            GetFuturesTdiSignalQuery.Verb,
            new InvalidOperationException("query failed"));

        await act.Should().NotThrowAsync();
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesTdiSignalQuery.Verb,
            Arg.Any<ServiceResult<FuturesTdiSignalReadModel?>>());
    }
}
