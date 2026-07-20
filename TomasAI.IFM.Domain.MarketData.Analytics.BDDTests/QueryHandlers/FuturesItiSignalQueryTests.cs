using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;
using TomasAI.IFM.Application.Storage;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.QueryHandlers;

/// <summary>
/// BDD-style tests for <see cref="FuturesItiSignalQueryActor"/> query handlers, exercising the query dispatch
/// pipeline (ReceiveAsync) end to end against a stubbed <see cref="IDbContextFactory"/>/<see cref="IMarketDataDbContext"/>,
/// covering happy paths and edge cases across Daily, Weekly, and Monthly time periods for every registered
/// futures ITI signal query.
/// </summary>
public class FuturesItiSignalQueryTests
{
    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    // Test helper to expose the protected ReceiveAsync method for BDD-style testing.
    class TestableFuturesItiSignalQueryActor(IDbContextFactory dbFactory, ILogger<FuturesItiSignalQueryActor> logger)
        : FuturesItiSignalQueryActor(dbFactory, logger)
    {
        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);
    }

    static TestableFuturesItiSignalQueryActor CreateActor(IDbContextFactory dbFactory)
        => new(dbFactory, Substitute.For<ILogger<FuturesItiSignalQueryActor>>());

    // FuturesItiSignalQueryActor.ReceiveAsync validates the state argument is non-null, so a dummy state
    // instance is supplied instead of default!/null in the Act phase of each test.
    static readonly IActorState DummyState = Substitute.For<IActorState>();

    static (IDbContextFactory DbFactory, IMarketDataDbContext MarketDataDb) CreateDbFactory()
    {
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        return (dbFactory, marketDataDb);
    }

    static IQueryActorContext CreateContext()
    {
        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);
        return context;
    }

    static TQuery WithSubject<TQuery>(TQuery query, string verb) where TQuery : IQuery
    {
        var subjectProp = typeof(TQuery).GetProperty(nameof(IQuery.Subject));
        var entityId = query.EntityId!.Format();
        var subject = new ActorSubject(ActorType.Query, FuturesItiSignalQueryActor.ActorName, verb, entityId);
        subjectProp!.SetValue(query, subject);
        return query;
    }

    static FuturesItiSignalV2ReadModel CreateReadModel(TradeTimePeriodType timePeriod)
        => SampleData.StartOfDayEvent.FuturesItiSignal! with { TimePeriod = timePeriod };

    // ───── GetFuturesItiSignalQuery — happy paths ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GetFuturesItiSignalQuery_GivenExistingSignal_WhenExecuted_ThenRepliesWithMatchingSignal(
        TradeTimePeriodType timePeriod)
    {
        // Arrange - Given an ITI signal exists for the requested contract/value date
        var (dbFactory, marketDataDb) = CreateDbFactory();
        var expected = CreateReadModel(timePeriod);
        marketDataDb.GetLastFuturesItiSignalAsync(SampleData.ContractId, SampleData.ValueDate).Returns(expected);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiSignalQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod), GetFuturesItiSignalQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply with the matching signal
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFuturesItiSignalQuery.Verb),
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel?>>(r => r.Success && r.Value == expected));
    }

    // ───── GetFuturesItiSignalQuery — edge cases ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GetFuturesItiSignalQuery_GivenNoSignalExists_WhenExecuted_ThenRepliesWithNullValue(
        TradeTimePeriodType timePeriod)
    {
        // Arrange - Given no ITI signal exists for the requested contract/value date (edge case)
        var (dbFactory, marketDataDb) = CreateDbFactory();
        marketDataDb.GetLastFuturesItiSignalAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((FuturesItiSignalV2ReadModel?)null);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiSignalQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod), GetFuturesItiSignalQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply with a successful result carrying a null value, without throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFuturesItiSignalQuery.Verb),
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel?>>(r => r.Success && r.Value == null));
    }

    [Fact]
    public async Task GetFuturesItiSignalQuery_GivenDifferentContractId_WhenExecuted_ThenRequestsDataForCorrectContract()
    {
        // Arrange - Given a different contract than the shared sample default
        const string otherContractId = "CLZ25";
        var (dbFactory, marketDataDb) = CreateDbFactory();
        var expected = CreateReadModel(TradeTimePeriodType.Daily) with { ContractId = otherContractId };
        marketDataDb.GetLastFuturesItiSignalAsync(otherContractId, SampleData.ValueDate).Returns(expected);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiSignalQuery(otherContractId, SampleData.ValueDate, TradeTimePeriodType.Daily), GetFuturesItiSignalQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then the correct contract is requested and returned
        await marketDataDb.Received(1).GetLastFuturesItiSignalAsync(otherContractId, SampleData.ValueDate);
        await marketDataDb.DidNotReceive().GetLastFuturesItiSignalAsync(SampleData.ContractId, SampleData.ValueDate);
        await context.Received(1).ReplyAsync(
            Arg.Any<ActorThreadId>(),
            Arg.Any<string>(),
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel?>>(r => r.Success && r.Value!.ContractId == otherContractId));
    }

    // ───── GetFuturesItiSignalDataQuery — happy paths ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GetFuturesItiSignalDataQuery_GivenAllChangeTypesExist_WhenExecuted_ThenRepliesWithAggregatedData(
        TradeTimePeriodType timePeriod)
    {
        // Arrange - Given trend direction, extreme, and reversal change signals all exist
        var (dbFactory, marketDataDb) = CreateDbFactory();
        var directionChange = CreateReadModel(timePeriod) with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged };
        var extremeChange = CreateReadModel(timePeriod) with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged };
        var reversalChange = CreateReadModel(timePeriod) with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendReversalChanged };
        marketDataDb.GetLastFuturesItiSignalTrendDirectionChangeAsync(SampleData.ContractId, SampleData.ValueDate).Returns(directionChange);
        marketDataDb.GetLastFuturesItiSignalTrendExtremeChangeAsync(SampleData.ContractId, SampleData.ValueDate).Returns(extremeChange);
        marketDataDb.GetLastFuturesItiSignalTrendReversalChangeAsync(SampleData.ContractId, SampleData.ValueDate).Returns(reversalChange);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiSignalDataQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod), GetFuturesItiSignalDataQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply with the aggregated data containing all three change signals
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFuturesItiSignalDataQuery.Verb),
            Arg.Is<ServiceResult<FuturesItiSignalDataReadModel?>>(r =>
                r.Success
                && r.Value!.TrendDirectionChange == directionChange
                && r.Value.TrendExtremeChange == extremeChange
                && r.Value.TrendReversalChange == reversalChange));
    }

    // ───── GetFuturesItiSignalDataQuery — edge cases ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GetFuturesItiSignalDataQuery_GivenNoChangesExist_WhenExecuted_ThenRepliesWithAllNullChanges(
        TradeTimePeriodType timePeriod)
    {
        // Arrange - Given no change-event signals exist for the requested contract/value date (edge case)
        var (dbFactory, marketDataDb) = CreateDbFactory();
        marketDataDb.GetLastFuturesItiSignalTrendDirectionChangeAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((FuturesItiSignalV2ReadModel?)null);
        marketDataDb.GetLastFuturesItiSignalTrendExtremeChangeAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((FuturesItiSignalV2ReadModel?)null);
        marketDataDb.GetLastFuturesItiSignalTrendReversalChangeAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((FuturesItiSignalV2ReadModel?)null);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiSignalDataQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod), GetFuturesItiSignalDataQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply successfully with all change snapshots null, without throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFuturesItiSignalDataQuery.Verb),
            Arg.Is<ServiceResult<FuturesItiSignalDataReadModel?>>(r =>
                r.Success
                && r.Value!.TrendDirectionChange == null
                && r.Value.TrendExtremeChange == null
                && r.Value.TrendReversalChange == null));
    }

    [Fact]
    public async Task GetFuturesItiSignalDataQuery_GivenOnlyTrendDirectionChangeExists_WhenExecuted_ThenRepliesWithPartialData()
    {
        // Arrange - Given only the trend direction change signal exists (edge case: partial data)
        var (dbFactory, marketDataDb) = CreateDbFactory();
        var directionChange = CreateReadModel(TradeTimePeriodType.Daily) with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged };
        marketDataDb.GetLastFuturesItiSignalTrendDirectionChangeAsync(SampleData.ContractId, SampleData.ValueDate).Returns(directionChange);
        marketDataDb.GetLastFuturesItiSignalTrendExtremeChangeAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((FuturesItiSignalV2ReadModel?)null);
        marketDataDb.GetLastFuturesItiSignalTrendReversalChangeAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((FuturesItiSignalV2ReadModel?)null);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiSignalDataQuery(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Daily), GetFuturesItiSignalDataQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply with only the trend direction change populated
        await context.Received(1).ReplyAsync(
            Arg.Any<ActorThreadId>(),
            Arg.Any<string>(),
            Arg.Is<ServiceResult<FuturesItiSignalDataReadModel?>>(r =>
                r.Success
                && r.Value!.TrendDirectionChange == directionChange
                && r.Value.TrendExtremeChange == null
                && r.Value.TrendReversalChange == null));
    }

    // ───── GetFuturesItiTrendDirectionChangedSignalsQuery — happy paths ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GetFuturesItiTrendDirectionChangedSignalsQuery_GivenMultipleSignals_WhenExecuted_ThenRepliesWithAllSignals(
        TradeTimePeriodType timePeriod)
    {
        // Arrange - Given multiple trend direction changed signals exist for the contract/value date
        var (dbFactory, marketDataDb) = CreateDbFactory();
        ICollection<FuturesItiSignalV2ReadModel> signals =
        [
            CreateReadModel(timePeriod),
            CreateReadModel(timePeriod) with { IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend }
        ];
        marketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(SampleData.ContractId, SampleData.ValueDate).Returns(signals);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiTrendDirectionChangedSignalsQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod), GetFuturesItiTrendDirectionChangedSignalsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply with both signals
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFuturesItiTrendDirectionChangedSignalsQuery.Verb),
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel[]>>(r => r.Success && r.Value!.Length == 2));
    }

    // ───── GetFuturesItiTrendDirectionChangedSignalsQuery — edge cases ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public async Task GetFuturesItiTrendDirectionChangedSignalsQuery_GivenNoSignalsExist_WhenExecuted_ThenRepliesWithEmptyArray(
        TradeTimePeriodType timePeriod)
    {
        // Arrange - Given no trend direction changed signals exist (edge case)
        var (dbFactory, marketDataDb) = CreateDbFactory();
        marketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(SampleData.ContractId, SampleData.ValueDate)
            .Returns((ICollection<FuturesItiSignalV2ReadModel>)[]);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiTrendDirectionChangedSignalsQuery(SampleData.ContractId, SampleData.ValueDate, timePeriod), GetFuturesItiTrendDirectionChangedSignalsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply with an empty array without throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFuturesItiTrendDirectionChangedSignalsQuery.Verb),
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel[]>>(r => r.Success && r.Value!.Length == 0));
    }

    [Fact]
    public async Task GetFuturesItiTrendDirectionChangedSignalsQuery_GivenSingleSignal_WhenExecuted_ThenRepliesWithSingleElementArray()
    {
        // Arrange - Given exactly one trend direction changed signal exists (boundary edge case)
        var (dbFactory, marketDataDb) = CreateDbFactory();
        ICollection<FuturesItiSignalV2ReadModel> signals = [CreateReadModel(TradeTimePeriodType.Weekly)];
        marketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(SampleData.ContractId, SampleData.ValueDate).Returns(signals);
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFuturesItiTrendDirectionChangedSignalsQuery(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Weekly), GetFuturesItiTrendDirectionChangedSignalsQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, DummyState, query);

        // Assert - Then reply with a single-element array
        await context.Received(1).ReplyAsync(
            Arg.Any<ActorThreadId>(),
            Arg.Any<string>(),
            Arg.Is<ServiceResult<FuturesItiSignalV2ReadModel[]>>(r => r.Success && r.Value!.Length == 1 && r.Value[0] == signals.First()));
    }
}
