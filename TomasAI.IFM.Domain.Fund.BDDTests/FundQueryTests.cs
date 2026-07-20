using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.BDDTests;

/// <summary>
/// BDD-style tests for <see cref="FundQueryActor"/> query handlers, exercising the query dispatch pipeline
/// (ReceiveAsync) end to end against a stubbed <see cref="IDbContextFactory"/>/<see cref="IFundDbContext"/>,
/// covering both happy paths and edge cases for every registered fund query.
/// </summary>
public class FundQueryTests
{
    // Test helper to expose the protected ReceiveAsync method for BDD-style testing.
    class TestableFundQueryActor(IDbContextFactory dbFactory, ILogger<FundQueryActor> logger)
        : FundQueryActor(dbFactory, logger)
    {
        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
            => await ReceiveAsync(context, state, query);
    }

    static TestableFundQueryActor CreateActor(IDbContextFactory dbFactory)
        => new(dbFactory, Substitute.For<ILogger<FundQueryActor>>());

    static (IDbContextFactory DbFactory, IFundDbContext FundDb) CreateDbFactory()
    {
        var fundDb = Substitute.For<IFundDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.FundDb.Returns(fundDb);
        return (dbFactory, fundDb);
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
        var entityId = (query.EntityId!).Format();
        var subject = new ActorSubject(ActorType.Query, FundQueryActor.ActorName, verb, entityId);
        subjectProp!.SetValue(query, subject);
        return query;
    }

    #region GetFundBalanceQuery

    [Fact]
    public async Task GetFundBalanceQuery_GivenFundWithBalance_WhenExecuted_ThenRepliesWithBalance()
    {
        // Arrange - Given a fund with a known balance
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundBalanceAsync(SampleData.Fund.FundId).Returns(Task.FromResult(SampleData.Fund.Balance));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundBalanceQuery(SampleData.Fund.FundId), GetFundBalanceQuery.Verb);

        // Act - When the query is executed
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert - Then reply with the correct balance
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundBalanceQuery.Verb),
            Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value!.Value == SampleData.Fund.Balance));
    }

    [Fact]
    public async Task GetFundBalanceQuery_GivenFundWithZeroBalance_WhenExecuted_ThenRepliesWithZero()
    {
        // Arrange - Given a fund whose balance is zero (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundBalanceAsync(Arg.Any<int>()).Returns(Task.FromResult(0.0m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundBalanceQuery(9999), GetFundBalanceQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert - Then reply with a zero balance without throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundBalanceQuery.Verb),
            Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value!.Value == 0.0m));
    }

    #endregion

    #region GetClosingFundBalanceQuery

    [Fact]
    public async Task GetClosingFundBalanceQuery_GivenValueDate_WhenExecuted_ThenRepliesWithClosingBalance()
    {
        // Arrange - Given a closing balance exists for the requested value date
        var (dbFactory, fundDb) = CreateDbFactory();
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        fundDb.GetClosingFundBalanceAsync(SampleData.Fund.FundId, valueDate).Returns(Task.FromResult(1500.25m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetClosingFundBalanceQuery(SampleData.Fund.FundId, valueDate), GetClosingFundBalanceQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetClosingFundBalanceQuery.Verb),
            Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value!.Value == 1500.25m));
    }

    [Fact]
    public async Task GetClosingFundBalanceQuery_GivenMinDateEdgeCase_WhenExecuted_ThenRepliesWithoutThrowing()
    {
        // Arrange - Given the minimum possible DateOnly value (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetClosingFundBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(0.0m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetClosingFundBalanceQuery(SampleData.Fund.FundId, DateOnly.MinValue), GetClosingFundBalanceQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetClosingFundBalanceQuery.Verb),
            Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value!.Value == 0.0m));
    }

    #endregion

    #region GetOpeningFundBalanceQuery

    [Fact]
    public async Task GetOpeningFundBalanceQuery_GivenValueDate_WhenExecuted_ThenRepliesWithOpeningBalance()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        fundDb.GetOpeningFundBalanceAsync(SampleData.Fund.FundId, valueDate).Returns(Task.FromResult(2000.00m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetOpeningFundBalanceQuery(SampleData.Fund.FundId, valueDate), GetOpeningFundBalanceQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetOpeningFundBalanceQuery.Verb),
            Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value!.Value == 2000.00m));
    }

    [Fact]
    public async Task GetOpeningFundBalanceQuery_GivenNonExistingFundId_WhenExecuted_ThenRepliesWithZeroBalance()
    {
        // Arrange - Given a fund id that does not exist (edge case, defaults to zero balance)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetOpeningFundBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(0.0m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetOpeningFundBalanceQuery(9999, DateOnly.FromDateTime(DateTime.UtcNow)), GetOpeningFundBalanceQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetOpeningFundBalanceQuery.Verb),
            Arg.Is<ServiceResult<FundBalanceReadModel>>(r => r.Success && r.Value!.Value == 0.0m));
    }

    #endregion

    #region GetFundDrawdownBalancesQuery

    [Fact]
    public async Task GetFundDrawdownBalancesQuery_GivenDateRange_WhenExecuted_ThenRepliesWithStartAndEndBalances()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        fundDb.GetFundStartingBalanceAsync(SampleData.Fund.FundId, startDate).Returns(Task.FromResult(1000.00m));
        fundDb.GetFundEndingBalanceAsync(SampleData.Fund.FundId, endDate).Returns(Task.FromResult(1200.00m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundDrawdownBalancesQuery(SampleData.Fund.FundId, startDate, endDate), GetFundDrawdownBalancesQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundDrawdownBalancesQuery.Verb),
            Arg.Is<ServiceResult<FundDrawdownBalancesReadModel>>(r =>
                r.Success && r.Value!.StartBalance == 1000.00m && r.Value.EndBalance == 1200.00m));
    }

    [Fact]
    public async Task GetFundDrawdownBalancesQuery_GivenStartDateEqualsEndDate_WhenExecuted_ThenRepliesWithSameDayBalances()
    {
        // Arrange - Given start and end date are the same day (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        var sameDate = DateOnly.FromDateTime(DateTime.UtcNow);
        fundDb.GetFundStartingBalanceAsync(SampleData.Fund.FundId, sameDate).Returns(Task.FromResult(500.00m));
        fundDb.GetFundEndingBalanceAsync(SampleData.Fund.FundId, sameDate).Returns(Task.FromResult(500.00m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundDrawdownBalancesQuery(SampleData.Fund.FundId, sameDate, sameDate), GetFundDrawdownBalancesQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundDrawdownBalancesQuery.Verb),
            Arg.Is<ServiceResult<FundDrawdownBalancesReadModel>>(r =>
                r.Success && r.Value!.StartBalance == 500.00m && r.Value.EndBalance == 500.00m));
    }

    #endregion

    #region GetFundIdFromOrderIdQuery

    [Fact]
    public async Task GetFundIdFromOrderIdQuery_GivenExistingOrderId_WhenExecuted_ThenRepliesWithFundId()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundIdFromOrderIdAsync(SampleData.FundOrder.OrderId).Returns(Task.FromResult(SampleData.Fund.FundId));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundIdFromOrderIdQuery(SampleData.FundOrder.OrderId), GetFundIdFromOrderIdQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundIdFromOrderIdQuery.Verb),
            Arg.Is<ServiceResult<ScalarReadModel<int>>>(r => r.Success && r.Value!.Value == SampleData.Fund.FundId));
    }

    [Fact]
    public async Task GetFundIdFromOrderIdQuery_GivenNonExistingOrderId_WhenExecuted_ThenRepliesWithDefaultValue()
    {
        // Arrange - Given an order id that does not resolve to any fund (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundIdFromOrderIdAsync(Arg.Any<int>()).Returns(Task.FromResult(0));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundIdFromOrderIdQuery(9999), GetFundIdFromOrderIdQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundIdFromOrderIdQuery.Verb),
            Arg.Is<ServiceResult<ScalarReadModel<int>>>(r => r.Success && r.Value!.Value == 0));
    }

    #endregion

    #region GetFundsQuery

    [Fact]
    public async Task GetFundsQuery_GivenExistingFunds_WhenExecuted_ThenRepliesWithAllFunds()
    {
        // Arrange - Given several funds exist
        var (dbFactory, fundDb) = CreateDbFactory();
        ICollection<FundReadModel> funds = [SampleData.Fund, SampleData.Fund with { FundId = 5678, Name = "SecondFund" }];
        fundDb.GetFundsAsync().Returns(Task.FromResult(funds));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundsQuery(), GetFundsQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundsQuery.Verb),
            Arg.Is<ServiceResult<FundReadModel[]>>(r => r.Success && r.Value!.Length == 2));
    }

    [Fact]
    public async Task GetFundsQuery_GivenNoFundsExist_WhenExecuted_ThenRepliesWithEmptyArray()
    {
        // Arrange - Given no funds exist (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundsAsync().Returns(Task.FromResult<ICollection<FundReadModel>>([]));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundsQuery(), GetFundsQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundsQuery.Verb),
            Arg.Is<ServiceResult<FundReadModel[]>>(r => r.Success && r.Value!.Length == 0));
    }

    #endregion

    #region GetFundOrdersQuery

    [Fact]
    public async Task GetFundOrdersQuery_GivenExistingOrders_WhenExecuted_ThenRepliesWithOrders()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        ICollection<FundOrderReadModel> orders = [SampleData.FundOrder];
        fundDb.GetFundOrdersAsync().Returns(Task.FromResult(orders));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundOrdersQuery(), GetFundOrdersQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundOrdersQuery.Verb),
            Arg.Is<ServiceResult<FundOrderReadModel[]>>(r =>
                r.Success && r.Value!.Length == 1 && r.Value[0].OrderId == SampleData.FundOrder.OrderId));
    }

    [Fact]
    public async Task GetFundOrdersQuery_GivenNoOrdersExist_WhenExecuted_ThenRepliesWithEmptyArray()
    {
        // Arrange - Given no fund orders exist (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundOrdersAsync().Returns(Task.FromResult<ICollection<FundOrderReadModel>>([]));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundOrdersQuery(), GetFundOrdersQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundOrdersQuery.Verb),
            Arg.Is<ServiceResult<FundOrderReadModel[]>>(r => r.Success && r.Value!.Length == 0));
    }

    #endregion

    #region GetFundOrderTradesQuery

    [Fact]
    public async Task GetFundOrderTradesQuery_GivenExistingTrades_WhenExecuted_ThenRepliesWithTrades()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        ICollection<FundOrderTradeReadModel> trades = [SampleData.FundOrderTrade];
        fundDb.GetFundOrderTradesAsync().Returns(Task.FromResult(trades));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundOrderTradesQuery(), GetFundOrderTradesQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundOrderTradesQuery.Verb),
            Arg.Is<ServiceResult<FundOrderTradeReadModel[]>>(r =>
                r.Success && r.Value!.Length == 1 && r.Value[0].TradeId == SampleData.FundOrderTrade.TradeId));
    }

    [Fact]
    public async Task GetFundOrderTradesQuery_GivenNoTradesExist_WhenExecuted_ThenRepliesWithEmptyArray()
    {
        // Arrange - Given no fund order trades exist (edge case)
        var (dbFactory, fundDb) = CreateDbFactory();
        fundDb.GetFundOrderTradesAsync().Returns(Task.FromResult<ICollection<FundOrderTradeReadModel>>([]));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundOrderTradesQuery(), GetFundOrderTradesQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundOrderTradesQuery.Verb),
            Arg.Is<ServiceResult<FundOrderTradeReadModel[]>>(r => r.Success && r.Value!.Length == 0));
    }

    #endregion

    #region GetFundPnlReportQuery

    [Fact]
    public async Task GetFundPnlReportQuery_GivenWinsAndLosses_WhenExecuted_ThenRepliesWithComputedReport()
    {
        // Arrange - Given both winning and losing orders exist within range
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        ICollection<FundOrderAmountReadModel> lossOrders = [new FundOrderAmountReadModel(SampleData.Fund.FundId, endDate, 1, -100m)];
        ICollection<FundOrderAmountReadModel> profitOrders = [new FundOrderAmountReadModel(SampleData.Fund.FundId, endDate, 2, 200m)];
        fundDb.GetFundLossOrdersAsync(SampleData.Fund.FundId, startDate, endDate).Returns(Task.FromResult(lossOrders));
        fundDb.GetFundProfitOrdersAsync(SampleData.Fund.FundId, startDate, endDate).Returns(Task.FromResult(profitOrders));
        fundDb.GetFundStartingBalanceAsync(SampleData.Fund.FundId, startDate).Returns(Task.FromResult(1000m));
        fundDb.GetFundEndingBalanceAsync(SampleData.Fund.FundId, endDate).Returns(Task.FromResult(1100m));
        fundDb.GetFundTradeCommissionAsync(SampleData.Fund.FundId, startDate, endDate).Returns(Task.FromResult(10m));
        fundDb.GetFundDailyBalancesAsync(SampleData.Fund.FundId, startDate, endDate).Returns(Task.FromResult<ICollection<FundDailyBalanceReadModel>>([]));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundPnlReportQuery(SampleData.Fund.FundId, startDate, endDate), GetFundPnlReportQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert - Then reply with a report reflecting the win/loss orders
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundPnlReportQuery.Verb),
            Arg.Is<ServiceResult<FundPnlReportReadModel>>(r =>
                r.Success && r.Value!.WinRate == 0.5 && r.Value.LossRate == 0.5 && r.Value.PnlAmount == 100m));
    }

    [Fact]
    public async Task GetFundPnlReportQuery_GivenNoOrdersInRange_WhenExecuted_ThenRepliesWithZeroedReport()
    {
        // Arrange - Given no orders exist in the requested range (edge case, avoids division by zero)
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        fundDb.GetFundLossOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ICollection<FundOrderAmountReadModel>>([]));
        fundDb.GetFundProfitOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ICollection<FundOrderAmountReadModel>>([]));
        fundDb.GetFundStartingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(0m));
        fundDb.GetFundEndingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(0m));
        fundDb.GetFundTradeCommissionAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(0m));
        fundDb.GetFundDailyBalancesAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ICollection<FundDailyBalanceReadModel>>([]));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundPnlReportQuery(SampleData.Fund.FundId, startDate, endDate), GetFundPnlReportQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert - Then reply with a report with zeroed rates instead of throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundPnlReportQuery.Verb),
            Arg.Is<ServiceResult<FundPnlReportReadModel>>(r =>
                r.Success && r.Value!.WinRate == 0 && r.Value.LossRate == 0 && r.Value.PnlAmount == 0m));
    }

    #endregion

    #region GetFundWinLossRatioQuery

    [Fact]
    public async Task GetFundWinLossRatioQuery_GivenWinsAndLosses_WhenExecuted_ThenRepliesWithComputedRatio()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        ICollection<FundOrderAmountReadModel> lossOrders = [new FundOrderAmountReadModel(SampleData.Fund.FundId, endDate, 1, -100m)];
        ICollection<FundOrderAmountReadModel> profitOrders = [new FundOrderAmountReadModel(SampleData.Fund.FundId, endDate, 2, 300m)];
        fundDb.GetFundLossOrdersAsync(SampleData.Fund.FundId, startDate, endDate).Returns(Task.FromResult(lossOrders));
        fundDb.GetFundProfitOrdersAsync(SampleData.Fund.FundId, startDate, endDate).Returns(Task.FromResult(profitOrders));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundWinLossRatioQuery(SampleData.Fund.FundId, startDate, endDate), GetFundWinLossRatioQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundWinLossRatioQuery.Verb),
            Arg.Is<ServiceResult<FundWinLossRatioReadModel>>(r => r.Success && r.Value!.WinLossRatio > 0));
    }

    [Fact]
    public async Task GetFundWinLossRatioQuery_GivenNoLossOrders_WhenExecuted_ThenRepliesWithZeroRatio()
    {
        // Arrange - Given there are no losing orders (edge case, avoids division by zero)
        var (dbFactory, fundDb) = CreateDbFactory();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        fundDb.GetFundLossOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ICollection<FundOrderAmountReadModel>>([]));
        ICollection<FundOrderAmountReadModel> profitOrders = [new FundOrderAmountReadModel(SampleData.Fund.FundId, endDate, 2, 300m)];
        fundDb.GetFundProfitOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(profitOrders));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundWinLossRatioQuery(SampleData.Fund.FundId, startDate, endDate), GetFundWinLossRatioQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert - Then reply with a zero win/loss ratio instead of throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundWinLossRatioQuery.Verb),
            Arg.Is<ServiceResult<FundWinLossRatioReadModel>>(r => r.Success && r.Value!.WinLossRatio == 0));
    }

    #endregion

    #region GetFundMaxProfitGeneratedQuery

    [Fact]
    public async Task GetFundMaxProfitGeneratedQuery_GivenFundActivity_WhenExecuted_ThenRepliesWithMaxProfitReport()
    {
        // Arrange
        var (dbFactory, fundDb) = CreateDbFactory();
        var tradeDate = DateOnly.FromDateTime(DateTime.UtcNow);
        ICollection<FundOrderAmountReadModel> profitOrders = [new FundOrderAmountReadModel(SampleData.Fund.FundId, tradeDate, 1, 300m)];
        ICollection<FundOrderAmountReadModel> lossOrders = [new FundOrderAmountReadModel(SampleData.Fund.FundId, tradeDate, 2, -100m)];
        fundDb.GetFundBalanceAsync(SampleData.Fund.FundId).Returns(Task.FromResult(SampleData.Fund.Balance));
        fundDb.GetFundProfitOrdersAsync(SampleData.Fund.FundId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(profitOrders));
        fundDb.GetFundLossOrdersAsync(SampleData.Fund.FundId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(lossOrders));
        fundDb.GetFundStartingBalanceAsync(SampleData.Fund.FundId, Arg.Any<DateOnly>()).Returns(Task.FromResult(1000m));
        fundDb.GetFundEndingBalanceAsync(SampleData.Fund.FundId, Arg.Any<DateOnly>()).Returns(Task.FromResult(1200m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundMaxProfitGeneratedQuery(SampleData.Fund.FundId, tradeDate), GetFundMaxProfitGeneratedQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundMaxProfitGeneratedQuery.Verb),
            Arg.Is<ServiceResult<FundMaxProfitGeneratedReadModel>>(r =>
                r.Success
                && r.Value!.FundBalance == SampleData.Fund.Balance
                && r.Value.FundProfitOrders.Count == 1
                && r.Value.FundLossOrders.Count == 1
                && r.Value.FundDrawdownBalances.StartBalance == 1000m
                && r.Value.FundDrawdownBalances.EndBalance == 1200m));
    }

    [Fact]
    public async Task GetFundMaxProfitGeneratedQuery_GivenTradeDateOnFirstOfMonth_WhenExecuted_ThenRepliesWithEmptyOrderCollections()
    {
        // Arrange - Given the trade date is the first day of the month (edge case for the order date range)
        var (dbFactory, fundDb) = CreateDbFactory();
        var tradeDate = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        fundDb.GetFundBalanceAsync(SampleData.Fund.FundId).Returns(Task.FromResult(0m));
        fundDb.GetFundProfitOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ICollection<FundOrderAmountReadModel>>([]));
        fundDb.GetFundLossOrdersAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ICollection<FundOrderAmountReadModel>>([]));
        fundDb.GetFundStartingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(0m));
        fundDb.GetFundEndingBalanceAsync(Arg.Any<int>(), Arg.Any<DateOnly>()).Returns(Task.FromResult(0m));
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var query = WithSubject(new GetFundMaxProfitGeneratedQuery(SampleData.Fund.FundId, tradeDate), GetFundMaxProfitGeneratedQuery.Verb);

        // Act
        await actor.InvokeReceiveAsync(context, default!, query);

        // Assert - Then reply with empty order collections without throwing
        await context.Received(1).ReplyAsync(
            Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
            Arg.Is<string>(v => v == GetFundMaxProfitGeneratedQuery.Verb),
            Arg.Is<ServiceResult<FundMaxProfitGeneratedReadModel>>(r =>
                r.Success && r.Value!.FundProfitOrders.Count == 0 && r.Value.FundLossOrders.Count == 0));
    }

    #endregion

    #region Cross-cutting edge cases

    [Fact]
    public async Task ReceiveAsync_GivenUnsupportedQueryType_WhenExecuted_ThenThrowsInvalidOperationException()
    {
        // Arrange - Given a query type that is not registered with the actor (edge case)
        var (dbFactory, _) = CreateDbFactory();
        var actor = CreateActor(dbFactory);
        var context = CreateContext();
        var unsupportedQuery = Substitute.For<IQuery>();

        // Act
        Func<Task> act = async () => await actor.InvokeReceiveAsync(context, default!, unsupportedQuery);

        // Assert - Then an InvalidOperationException is thrown identifying the actor and query
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{FundQueryActor.ActorName}*");
    }

    #endregion
}
