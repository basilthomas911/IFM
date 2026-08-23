using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.Query.Api;

public class ActorMarketDataQueryApiTests
{
    [Fact]
    public async Task TradingDaysUseDirectStorageAndReturnTypedSuccess()
    {
        var (api, db) = CreateApi();
        var startDate = new DateOnly(2026, 1, 5);
        var endDate = startDate.AddDays(2);
        db.GetTradingDayCountAsync(startDate, endDate, MarketType.Futures, CurrencyType.USD)
            .Returns(2);

        var result = await api.GetTradingDaysAsync(
            startDate, endDate, MarketType.Futures, CurrencyType.USD);

        result.Success.Should().BeTrue();
        result.Value!.Value.Should().Be(2);
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("market data unavailable");
        db.GetTradingDayCountAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<MarketType>(), Arg.Any<CurrencyType>())
            .Returns(_ => Task.FromException<int>(exception));

        var result = await api.GetTradingDaysAsync(
            new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 6),
            MarketType.Futures, CurrencyType.USD);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetTradingDaysQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task OptionContractIdsUseOneBulkReadAndPreserveInputOrder()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var securitiesDb = Substitute.For<ISecuritiesDbContext>();
        dbFactory.SecuritiesDb.Returns(securitiesDb);
        securitiesDb.GetFuturesOptionContractsByIdsAsync(Arg.Any<ICollection<string>>())
            .Returns(
            [
                new FuturesOptionContractReadModel { ContractId = "A" },
                new FuturesOptionContractReadModel { ContractId = "C" }
            ]);
        var api = CreateContext(dbFactory);

        var result = await api.GetFuturesOptionContractIdsAsync(["C", "B", "A", "C"]);

        result.Success.Should().BeTrue();
        result.Value.Should().Equal("C", "A", "C");
        await securitiesDb.Received(1)
            .GetFuturesOptionContractsByIdsAsync(
                Arg.Is<ICollection<string>>(ids => ids.Count == 3));
        await securitiesDb.DidNotReceive()
            .GetFuturesOptionContractAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task IronCondorStartsIndependentReadsBeforeAwaitingTheirResults()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var securitiesDb = Substitute.For<ISecuritiesDbContext>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.SecuritiesDb.Returns(securitiesDb);
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var underlyingSource = new TaskCompletionSource<FuturesContractV2ReadModel?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var optionsSource = new TaskCompletionSource<ICollection<FuturesOptionContractReadModel>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var yieldCurveSource = new TaskCompletionSource<YieldCurveRateReadModel?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var tradingDaysSource = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        securitiesDb.GetFuturesContractAsync("U").Returns(underlyingSource.Task);
        securitiesDb.GetFuturesOptionContractsByIdsAsync(Arg.Any<ICollection<string>>())
            .Returns(optionsSource.Task);
        marketDataDb.GetLastYieldCurveRateAsync().Returns(yieldCurveSource.Task);
        marketDataDb.GetTradingDayCountAsync(
                Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<MarketType>(), Arg.Any<CurrencyType>())
            .Returns(tradingDaysSource.Task);
        var api = CreateContext(dbFactory);

        var pendingResult = api.GetIronCondorMarketDataAsync(
            "U", "SP", "LP", "SC", "LC",
            new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5),
            MarketType.Futures, CurrencyType.USD);

        await securitiesDb.Received(1).GetFuturesContractAsync("U");
        await securitiesDb.Received(1)
            .GetFuturesOptionContractsByIdsAsync(Arg.Any<ICollection<string>>());
        await marketDataDb.Received(1).GetLastYieldCurveRateAsync();
        await marketDataDb.Received(1).GetTradingDayCountAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
            MarketType.Futures, CurrencyType.USD);

        underlyingSource.SetResult(new FuturesContractV2ReadModel { ContractId = "U" });
        optionsSource.SetResult(
        [
            new FuturesOptionContractReadModel { ContractId = "SP" },
            new FuturesOptionContractReadModel { ContractId = "LP" },
            new FuturesOptionContractReadModel { ContractId = "SC" },
            new FuturesOptionContractReadModel { ContractId = "LC" }
        ]);
        yieldCurveSource.SetResult(new YieldCurveRateReadModel { OneMonth = 5 });
        tradingDaysSource.SetResult(20);

        var result = await pendingResult;
        result.Success.Should().BeTrue();
        result.Value!.TradingDays.Should().Be(20);
    }

    [Fact]
    public async Task IronCondorCancellationReachesBothStoresAndIsNotConvertedToFailure()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var securitiesDb = Substitute.For<ISecuritiesDbContext>();
        var marketDataDb = Substitute.For<IMarketDataDbContext>();
        dbFactory.SecuritiesDb.Returns(securitiesDb);
        dbFactory.MarketDataDb.Returns(marketDataDb);
        var underlyingSource = new TaskCompletionSource<FuturesContractV2ReadModel?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var optionsSource = new TaskCompletionSource<ICollection<FuturesOptionContractReadModel>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var yieldCurveSource = new TaskCompletionSource<YieldCurveRateReadModel?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var tradingDaysSource = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        securitiesDb.GetFuturesContractAsync("U", cancellation.Token)
            .Returns(underlyingSource.Task);
        securitiesDb.GetFuturesOptionContractsByIdsAsync(
                Arg.Any<ICollection<string>>(), cancellation.Token)
            .Returns(optionsSource.Task);
        marketDataDb.GetLastYieldCurveRateAsync(cancellation.Token)
            .Returns(yieldCurveSource.Task);
        marketDataDb.GetTradingDayCountAsync(
                Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<MarketType>(), Arg.Any<CurrencyType>(), cancellation.Token)
            .Returns(tradingDaysSource.Task);
        var api = CreateContext(dbFactory);

        var pendingResult = api.GetIronCondorMarketDataAsync(
            "U", "SP", "LP", "SC", "LC",
            new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5),
            MarketType.Futures, CurrencyType.USD, cancellation.Token);

        await securitiesDb.Received(1).GetFuturesContractAsync("U", cancellation.Token);
        await securitiesDb.Received(1).GetFuturesOptionContractsByIdsAsync(
            Arg.Any<ICollection<string>>(), cancellation.Token);
        await marketDataDb.Received(1).GetLastYieldCurveRateAsync(cancellation.Token);
        await marketDataDb.Received(1).GetTradingDayCountAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
            MarketType.Futures, CurrencyType.USD, cancellation.Token);

        cancellation.Cancel();
        underlyingSource.SetResult(new FuturesContractV2ReadModel { ContractId = "U" });
        optionsSource.SetResult(
        [
            new FuturesOptionContractReadModel { ContractId = "SP" },
            new FuturesOptionContractReadModel { ContractId = "LP" },
            new FuturesOptionContractReadModel { ContractId = "SC" },
            new FuturesOptionContractReadModel { ContractId = "LC" }
        ]);
        yieldCurveSource.SetResult(new YieldCurveRateReadModel { OneMonth = 5 });
        tradingDaysSource.SetResult(20);

        Func<Task> act = async () => await pendingResult;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    static (IMarketDataQueryContext Api, IMarketDataDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        dbFactory.SecuritiesDb.Returns(Substitute.For<ISecuritiesDbContext>());
        return (CreateContext(dbFactory), db);
    }

    static IMarketDataQueryContext CreateContext(IDbContextFactory dbFactory)
    {
        var context = Substitute.For<IMarketDataQueryContext>();
        context.DbFactory.Returns(dbFactory);
        return context;
    }
}
