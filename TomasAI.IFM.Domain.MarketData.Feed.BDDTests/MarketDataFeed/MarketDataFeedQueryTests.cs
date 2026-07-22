using FluentAssertions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.MarketDataFeed;

public class MarketDataFeedQueryTests : IClassFixture<MarketDataFeedBddFixture>
{
    readonly MarketDataFeedBddFixture _fixture;

    public MarketDataFeedQueryTests(MarketDataFeedBddFixture fixture) => _fixture = fixture;

    public static TheoryData<string> QueryKinds => new()
    {
        "OptionContract", "OptionSpread", "Risk", "IronCondor", "NormalCurve", "OptionQuoteId", "StreamingRequestId"
    };

    [Theory]
    [MemberData(nameof(QueryKinds))]
    public void Given_AValidMarketDataFeedQueryMessage_When_Parsed_Then_TheQueryAndMessageInfoArePreserved(string kind)
    {
        var actor = _fixture.CreateMarketDataFeedQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery(kind);

        var parsed = actor.InvokeParseMessage(context, CreateMessage(query));

        parsed.GetType().Should().Be(query.GetType());
        parsed.Subject.Should().Be(query.Subject);
        context.Received(1).SetMessageInfo(query.Subject.ThreadId, query.Subject.Verb, Arg.Any<ActorMessageInfo>());
    }

    [Theory]
    [InlineData(ActorType.Command, MarketDataFeedQueryActor.ActorName, GetFuturesOptionContractQuery.Verb)]
    [InlineData(ActorType.Query, "WrongActor", GetFuturesOptionContractQuery.Verb)]
    [InlineData(ActorType.Query, MarketDataFeedQueryActor.ActorName, "UnknownVerb")]
    public void Given_AnInvalidQuerySubject_When_Parsed_Then_ItIsRejected(ActorType type, string name, string verb)
    {
        var query = CreateQuery("OptionContract");
        var message = new NatsMsg<byte[]>
        {
            Subject = new ActorSubject(type, name, verb, query.EntityId.Format()).ToString(),
            Data = Serialize(query)
        };

        var act = () => _fixture.CreateMarketDataFeedQueryActor()
            .InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Given_CorruptQueryData_When_Parsed_Then_DeserializationFails()
    {
        var query = CreateQuery("OptionContract");
        var message = new NatsMsg<byte[]> { Subject = query.Subject.ToString(), Data = [0, 1, 255] };

        var act = () => _fixture.CreateMarketDataFeedQueryActor()
            .InvokeParseMessage(Substitute.For<IQueryActorContext>(), message);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public async Task Given_TheBrokerFindsAnOptionContract_When_Queried_Then_TheContractIsReturnedAndStreamIsCleanedUp()
    {
        var snapshot = CreateSnapshot();
        var contract = SampleData.FuturesOptionContracts[0];
        snapshot.StreamIds.Add(contract.ContractId).Returns(41);
        snapshot.GetFuturesOptionContractAsync(41, Arg.Any<FuturesOptionContractReadModel>()).Returns(contract);
        var actor = _fixture.CreateMarketDataFeedQueryActor(snapshot);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesOptionContractQuery)CreateQuery("OptionContract");

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, GetFuturesOptionContractQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionContractReadModel>>(result => result.Success && result.Value == contract));
        snapshot.StreamIds.Received(1).Remove(41);
        snapshot.Received(1).Stop();
    }

    [Fact]
    public async Task Given_TwoOptionLegs_When_TheSpreadIsQueried_Then_BothPricesAreReturnedAndStreamsAreCleanedUp()
    {
        var snapshot = CreateSnapshot();
        var shortContract = SampleData.FuturesOptionContracts[0];
        var longContract = SampleData.FuturesOptionContracts[1];
        snapshot.GetFuturesOptionSpreadAsync(shortContract, longContract).Returns((shortContract, longContract));
        snapshot.StreamIds.Add(shortContract.ContractId).Returns(51);
        snapshot.StreamIds.Add(longContract.ContractId).Returns(52);
        snapshot.GetFuturesOptionPriceAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<FuturesOptionContractReadModel>(),
                Arg.Any<Action<FuturesOptionTickDataV2ReadModel>>())
            .Returns(call =>
            {
                call.Arg<Action<FuturesOptionTickDataV2ReadModel>>()(SampleData.EsOptionTickData);
                return Task.CompletedTask;
            });
        var actor = _fixture.CreateMarketDataFeedQueryActor(snapshot);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetFuturesOptionSpreadDataQuery)CreateQuery("OptionSpread");

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, GetFuturesOptionSpreadDataQuery.Verb,
            Arg.Is<ServiceResult<FuturesOptionSpreadDataReadModel>>(result => result.Success && result.Value != null));
        snapshot.StreamIds.Received(1).Remove(51);
        snapshot.StreamIds.Received(1).Remove(52);
    }

    [Fact]
    public async Task Given_TheBrokerCannotResolveSpreadContracts_When_Queried_Then_TheFailurePropagatesAndTheSessionStops()
    {
        var snapshot = CreateSnapshot();
        snapshot.GetFuturesOptionSpreadAsync(Arg.Any<FuturesOptionContractReadModel>(), Arg.Any<FuturesOptionContractReadModel>())
            .Returns(((FuturesOptionContractReadModel?)null, (FuturesOptionContractReadModel?)null));
        var actor = _fixture.CreateMarketDataFeedQueryActor(snapshot);

        var act = () => actor.InvokeReceiveAsync(Substitute.For<IQueryActorContext>(), CreateQuery("OptionSpread")).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Unknown futures option contract*");
        snapshot.Received().Stop();
    }

    [Theory]
    [InlineData(TradeType.ShortIronCondor)]
    [InlineData(TradeType.LongIronCondor)]
    public async Task Given_CurrentEodData_When_RiskIsQueried_Then_AKnownRiskPositionIsReturned(TradeType tradeType)
    {
        var (factory, database) = CreateDatabase();
        database.GetCurrentFuturesEodDataAsync(SampleData.ValueDate).Returns(SampleData.EodDataToday);
        var actor = _fixture.CreateMarketDataFeedQueryActor(dbFactory: factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = Route(new GetFuturesRiskPositionTypeQuery(SampleData.ValueDate, tradeType));

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, GetFuturesRiskPositionTypeQuery.Verb,
            Arg.Is<ServiceResult<RiskPositionTypeReadModel>>(result => result.Success && result.Value != null));
    }

    [Fact]
    public async Task Given_StoredLegTicks_When_AnIronCondorIsQueried_Then_AllFiveMarketInputsAreReturnedOnTheCorrectVerb()
    {
        var (factory, database) = CreateDatabase();
        database.GetLastFuturesTickDataAsync(Arg.Any<string>(), SampleData.ValueDate).Returns(SampleData.EsTickData);
        database.GetLastFuturesOptionTickDataAsync(Arg.Any<string>(), SampleData.ValueDate).Returns(SampleData.EsOptionTickData);
        var actor = _fixture.CreateMarketDataFeedQueryActor(dbFactory: factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetIronCondorMarketDataFeedQuery)CreateQuery("IronCondor");

        await actor.InvokeReceiveAsync(context, query);

        await database.Received(4).GetLastFuturesOptionTickDataAsync(Arg.Any<string>(), SampleData.ValueDate);
        await context.Received(1).ReplyAsync(query.Subject.ThreadId, GetIronCondorMarketDataFeedQuery.Verb,
            Arg.Is<ServiceResult<IronCondorMarketDataFeedReadModel>>(result => result.Success && result.Value != null));
    }

    [Fact]
    public async Task Given_AStoredNormalCurve_When_Queried_Then_ItIsReturnedOnTheCorrectVerb()
    {
        var (factory, database) = CreateDatabase();
        database.GetNormalCurveTableAsync().Returns(SampleData.NormCurveData);
        var actor = _fixture.CreateMarketDataFeedQueryActor(dbFactory: factory);
        var context = Substitute.For<IQueryActorContext>();
        var query = (GetNormalCurveTableQuery)CreateQuery("NormalCurve");

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, GetNormalCurveTableQuery.Verb,
            Arg.Is<ServiceResult<NormalCurveTableReadModel>>(result => result.Success && result.Value == SampleData.NormCurveData));
    }

    [Theory]
    [InlineData("OptionQuoteId", 701)]
    [InlineData("StreamingRequestId", 702)]
    public async Task Given_AnAtomicSequence_When_AnIdentifierIsQueried_Then_TheNextIdIsReturned(string kind, long nextId)
    {
        var redis = Substitute.For<IRedisCache>();
        redis.Increment(Arg.Any<string>()).Returns(nextId);
        var blackboard = Substitute.For<IBlackboardService>();
        blackboard.SequenceCounter.Returns(new SequenceCounterModel(redis));
        var actor = _fixture.CreateMarketDataFeedQueryActor(blackboard: blackboard);
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery(kind);

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            Arg.Is<ServiceResult<ScalarValue<int>>>(result => result.Success && result.Value!.Value == nextId));
    }

    [Fact]
    public async Task Given_MissingOrUnsupportedReceiveInputs_When_Queried_Then_TheyAreRejected()
    {
        var actor = _fixture.CreateMarketDataFeedQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery("NormalCurve");
        var unsupported = Substitute.For<IQuery>();

        await ((Func<Task>)(() => actor.InvokeReceiveAsync(null!, query).AsTask())).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, null!).AsTask())).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => actor.InvokeReceiveAsync(context, unsupported).AsTask())).Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(QueryKinds))]
    public async Task Given_AKnownQueryFailure_When_Handled_Then_OneTypedFailureReplyIsSent(string kind)
    {
        var actor = _fixture.CreateMarketDataFeedQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = CreateQuery(kind);

        await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, query.Subject.Verb, new TimeoutException("timed out"));

        context.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IQueryActorContext.ReplyAsync)).Should().Be(1);
    }

    [Fact]
    public async Task Given_AnUnknownQueryFailure_When_Handled_Then_AFallbackFailureIsSent()
    {
        var actor = _fixture.CreateMarketDataFeedQueryActor();
        var context = Substitute.For<IQueryActorContext>();
        var query = Substitute.For<IQuery>();
        query.Subject.Returns(new ActorSubject(ActorType.Query, MarketDataFeedQueryActor.ActorName, "Unknown", "entity"));

        await actor.InvokeOnExceptionAsync(context, query.Subject.ThreadId, query, "Unknown", new Exception("unknown failure"));

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, "Unknown",
            Arg.Is<ServiceFailed<ActorEntityId>>(result => !result.Success && result.ErrorCode == 9999));
    }

    static IMarketDataSnapshotApi CreateSnapshot()
    {
        var snapshot = Substitute.For<IMarketDataSnapshotApi>();
        snapshot.StreamIds.Returns(Substitute.For<IStreamIdCollection>());
        return snapshot;
    }

    static (IDbContextFactory Factory, IMarketDataDbContext Database) CreateDatabase()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var database = Substitute.For<IMarketDataDbContext>();
        factory.MarketDataDb.Returns(database);
        return (factory, database);
    }

    static IQuery CreateQuery(string kind) => kind switch
    {
        "OptionContract" => Route(new GetFuturesOptionContractQuery(
            SampleData.FuturesOptionContracts[0].ContractId, SampleData.FuturesOptionContracts[0])),
        "OptionSpread" => Route(new GetFuturesOptionSpreadDataQuery(
            SampleData.ValueDate, SampleData.OptionMaturityDate, 5450.25, SampleData.RiskFreeRate, 0.25,
            new FuturesOptionContractsReadModel(SampleData.FuturesOptionContracts))),
        "Risk" => Route(new GetFuturesRiskPositionTypeQuery(SampleData.ValueDate, TradeType.ShortIronCondor)),
        "IronCondor" => Route(new GetIronCondorMarketDataFeedQuery(
            SampleData.EsContract.ContractId, "ES-P-S", "ES-P-L", "ES-C-S", "ES-C-L", SampleData.ValueDate)),
        "NormalCurve" => Route(new GetNormalCurveTableQuery()),
        "OptionQuoteId" => Route(new GetOptionQuoteIdQuery("option-quote")),
        "StreamingRequestId" => Route(new GetStreamingRequestIdQuery("stream-request")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    static T Route<T>(T query) where T : IQuery
    {
        var subject = new ActorSubject(ActorType.Query, MarketDataFeedQueryActor.ActorName, Verb(query), query.EntityId.Format());
        switch (query)
        {
            case GetFuturesOptionContractQuery value: value.Subject = subject; break;
            case GetFuturesOptionSpreadDataQuery value: value.Subject = subject; break;
            case GetFuturesRiskPositionTypeQuery value: value.Subject = subject; break;
            case GetIronCondorMarketDataFeedQuery value: value.Subject = subject; break;
            case GetNormalCurveTableQuery value: value.Subject = subject; break;
            case GetOptionQuoteIdQuery value: value.Subject = subject; break;
            case GetStreamingRequestIdQuery value: value.Subject = subject; break;
        }
        return query;
    }

    static string Verb(IQuery query) => query switch
    {
        GetFuturesOptionContractQuery => GetFuturesOptionContractQuery.Verb,
        GetFuturesOptionSpreadDataQuery => GetFuturesOptionSpreadDataQuery.Verb,
        GetFuturesRiskPositionTypeQuery => GetFuturesRiskPositionTypeQuery.Verb,
        GetIronCondorMarketDataFeedQuery => GetIronCondorMarketDataFeedQuery.Verb,
        GetNormalCurveTableQuery => GetNormalCurveTableQuery.Verb,
        GetOptionQuoteIdQuery => GetOptionQuoteIdQuery.Verb,
        GetStreamingRequestIdQuery => GetStreamingRequestIdQuery.Verb,
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };

    static NatsMsg<byte[]> CreateMessage(IQuery query)
        => new() { Subject = query.Subject.ToString(), Data = Serialize(query) };

    static byte[] Serialize(IQuery query) => query switch
    {
        GetFuturesOptionContractQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetFuturesOptionSpreadDataQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetFuturesRiskPositionTypeQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetIronCondorMarketDataFeedQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetNormalCurveTableQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetOptionQuoteIdQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        GetStreamingRequestIdQuery value => ActorExtensions.DataSerializer!.Serialize(value),
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };
}
