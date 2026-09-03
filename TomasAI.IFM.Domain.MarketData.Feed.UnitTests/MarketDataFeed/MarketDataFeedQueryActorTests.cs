using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Query;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.MarketDataFeed;

public class MarketDataFeedQueryActorTests : IClassFixture<MarketDataFeedTestFixture>
{
    readonly MarketDataFeedTestFixture _fixture;

    public MarketDataFeedQueryActorTests(MarketDataFeedTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableMarketDataFeedQueryActor : MarketDataFeedQueryActor
    {
        public IMarketDataFeedQueryContext Context { get; }

        public TestableMarketDataFeedQueryActor(
            ApplicationMarketDataApi marketDataApi,
            ISequenceIdGenerator sequenceIdGenerator,
            IDbContextFactory dbFactory,
            ILogger<MarketDataFeedQueryActor> logger)
            : this(TypedActorContextFactory.Query(dbFactory, logger)) { }

        public TestableMarketDataFeedQueryActor(IMarketDataFeedQueryContext context)
            : base(context) => Context = context;

        public IQuery InvokeParseMessage(IQueryActorContext<MarketDataFeedQueryActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext<MarketDataFeedQueryActor> context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext<MarketDataFeedQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);


    }

    [Fact]
    [Trait("TestType", "Integration")]
    public async Task RuntimeStatusQuery_ReturnsTypedApplicationFeedState()
    {
        var expected = new MarketDataFeedRuntimeStatusReadModel
        {
            IsRunning = true,
            ActiveValueDate = new DateOnly(2026, 9, 1),
            ObservedAtUtc = new DateTimeOffset(2026, 8, 31, 22, 0, 0, TimeSpan.Zero)
        };
        var marketDataApi = Substitute.For<ApplicationMarketDataApi>();
        marketDataApi.GetRuntimeStatus().Returns(expected);
        var context = Substitute.For<IMarketDataFeedQueryContext>();
        context.MarketDataApi.Returns(marketDataApi);
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.SequenceIdGenerator.Returns(Substitute.For<ISequenceIdGenerator>());
        context.Logger.Returns(Substitute.For<ILogger<MarketDataFeedQueryActor>>());
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, MarketDataFeedQueryActor.ActorName));
        var actor = new TestableMarketDataFeedQueryActor(context);
        var query = new GetMarketDataFeedRuntimeStatusQuery
        {
            Subject = new ActorSubject(
                ActorType.Query,
                GetMarketDataFeedRuntimeStatusQuery.Actor,
                GetMarketDataFeedRuntimeStatusQuery.Verb,
                "runtime-status")
        };

        await actor.InvokeReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetMarketDataFeedRuntimeStatusQuery.Verb,
            Arg.Is<ServiceResult<MarketDataFeedRuntimeStatusReadModel>>(result =>
                result.Success && ReferenceEquals(result.Value, expected)));
    }

    [Fact]
    [Trait("TestType", "Integration")]
    public async Task Databento_queries_return_immutable_readiness_contracts_and_history_without_polling_native()
    {
        var valueDate = new DateOnly(2026, 9, 2);
        var lifecycle = Substitute.For<IMarketDataLifecycleRequests>();
        lifecycle.Current.Returns(new DatabentoLifecycleSnapshot
        {
            State = DatabentoLifecycleState.Healthy, StateRevision = 3, ValueDate = valueDate,
            CorrelationId = Guid.NewGuid(), NativeGeneration = Guid.NewGuid(), RecoveryAttempt = 0,
            Reason = "ready", ChangedOnUtc = DateTime.UtcNow,
            LastObservation = Observation(valueDate)
        });
        var store = Substitute.For<IMarketDataServiceStore>();
        store.ListAssignmentsAsync(Arg.Any<CancellationToken>()).Returns([
            Assignment(DatabentoContractRole.EsQuarterly, "ES"),
            Assignment(DatabentoContractRole.VxFrontMonth, "VX1"),
            Assignment(DatabentoContractRole.VxSecondMonth, "VX2")]);
        store.ListObservationsAsync(valueDate, null, 25, Arg.Any<CancellationToken>())
            .Returns([Observation(valueDate)]);
        var context = Substitute.For<IMarketDataFeedQueryContext>();
        context.MarketDataApi.Returns(Substitute.For<ApplicationMarketDataApi>());
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.SequenceIdGenerator.Returns(Substitute.For<ISequenceIdGenerator>());
        context.MarketDataLifecycle.Returns(lifecycle);
        context.MarketDataServiceStore.Returns(store);
        context.Logger.Returns(Substitute.For<ILogger<MarketDataFeedQueryActor>>());
        context.ActorId.Returns(new ActorMailboxId(ActorType.Query, MarketDataFeedQueryActor.ActorName));
        var actor = new TestableMarketDataFeedQueryActor(context);
        var readiness = new GetDatabentoReadinessQuery
        {
            Subject = new ActorSubject(ActorType.Query, MarketDataFeedQueryActor.ActorName,
                GetDatabentoReadinessQuery.Verb, "readiness")
        };
        var contracts = new GetDatabentoCurrentContractsQuery
        {
            Subject = new ActorSubject(ActorType.Query, MarketDataFeedQueryActor.ActorName,
                GetDatabentoCurrentContractsQuery.Verb, "contracts")
        };
        var history = new GetDatabentoWatchdogHistoryQuery(valueDate, null, 25)
        {
            Subject = new ActorSubject(ActorType.Query, MarketDataFeedQueryActor.ActorName,
                GetDatabentoWatchdogHistoryQuery.Verb, "history")
        };

        await actor.InvokeReceiveAsync(context, readiness);
        await actor.InvokeReceiveAsync(context, contracts);
        await actor.InvokeReceiveAsync(context, history);

        await context.Received(1).ReplyAsync(readiness.Subject.ThreadId, GetDatabentoReadinessQuery.Verb,
            Arg.Is<ServiceResult<DatabentoReadinessReadModel>>(result => result.Success && result.Value!.CoreReady));
        await context.Received(1).ReplyAsync(contracts.Subject.ThreadId, GetDatabentoCurrentContractsQuery.Verb,
            Arg.Is<ServiceResult<DatabentoContractAssignmentReadModel[]>>(result => result.Value!.Length == 3));
        await context.Received(1).ReplyAsync(history.Subject.ThreadId, GetDatabentoWatchdogHistoryQuery.Verb,
            Arg.Is<ServiceResult<DatabentoWatchdogObservationReadModel[]>>(result => result.Value!.Length == 1));
        await lifecycle.DidNotReceive().ProbeAsync(Arg.Any<CancellationToken>());
    }

    static FuturesRolloverContractAssignment Assignment(DatabentoContractRole role, string id) => new()
    {
        ContractRole = role, RootSymbol = role == DatabentoContractRole.EsQuarterly ? "ES" : "VX",
        ContractId = id, Description = id, LocalSymbol = id, SecurityType = "FUT", Currency = "USD",
        Exchange = "CME", Multiplier = "1", LastTradeDate = new(2026, 12, 18),
        NextRolloverDate = new(2026, 12, 18), SourceContractHash = new string('a', 64), RowVersion = 1,
        CreatedOnUtc = DateTime.UtcNow, CreatedBy = "test", UpdatedOnUtc = DateTime.UtcNow, UpdatedBy = "test"
    };

    static DatabentoWatchdogObservation Observation(DateOnly valueDate) => new()
    {
        WatchdogStatusLogId = 1, ObservationId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(),
        ValueDate = valueDate, ObservedOnUtc = DateTime.UtcNow, OperationReason = DatabentoOperationReason.WatchdogPoll,
        MajorStatus = DatabentoMajorStatus.Up, DisplayHealth = DatabentoDisplayHealth.Green,
        CoreContractsReady = true, RecoveryAttempt = 0, NativeBackend = "Cpp", NativeAbiVersion = 3,
        NativeGeneration = Guid.NewGuid(), FeedStatusDetails = [], RowVersion = 1
    };

 
}
