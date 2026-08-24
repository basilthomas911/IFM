using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Query.Api;

public class ActorMarketDataAnalyticsQueryApiTests
{
    const string ContractId = "ESM6";
    const int PeriodLength = 14;
    const TimeFrameType TimePeriod = TimeFrameType.Daily;

    [Fact]
    public void AllDirectActorQueriesExposeCancellationAwareOverloads()
    {
        string[] methodNames =
        [
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesTradeSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetLastFuturesTradeSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesTradeSignalBySymbolAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesTradeSignalIdsAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesRsiSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesRsiDailySignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesTrendDirectionFromRSISignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesTdiSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesItiSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesItiTrendDirectionChangedSignalsAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesItiSignalDataAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesItiMDIDistributionAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesItiMDIDistributionByTrendAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesItiSignalMDIAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesItiSignalMDIByTrendAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesAtrSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesAtrDailySignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesAdxSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesAdxDailySignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesMacdSignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesMacdDailySignalAsync)
        ];

        var methods = typeof(MarketDataAnalyticsQueryExtensions).GetMethods();
        foreach (var methodName in methodNames)
        {
            methods.Should().Contain(method =>
                method.Name == methodName &&
                method.GetParameters().Last().ParameterType == typeof(CancellationToken));
        }
    }

    [Fact]
    public async Task PreCanceledDirectQueryDoesNotStartStorageWork()
    {
        var (api, db) = CreateApi();
        var valueDate = new DateOnly(2026, 1, 5);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => api.GetFuturesTradeSignalIdsAsync(valueDate, cancellation.Token));

        db.DidNotReceive().GetFuturesTradeSignalIdByValueDateAsync(
            Arg.Any<DateOnly>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StorageCancellationIsNotConvertedIntoAServiceFailure()
    {
        var (api, db) = CreateApi();
        var valueDate = new DateOnly(2026, 1, 5);
        using var cancellation = new CancellationTokenSource();
        db.GetFuturesTradeSignalIdByValueDateAsync(valueDate, cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<ICollection<FuturesTradeSignalId>>(cancellation.Token);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => api.GetFuturesTradeSignalIdsAsync(valueDate, cancellation.Token));
    }

    [Fact]
    public void DailySignalQueriesAreActorOnlyContractMethods()
    {
        string[] methodNames =
        [
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesAdxDailySignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesAtrDailySignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesMacdDailySignalAsync),
            nameof(MarketDataAnalyticsQueryExtensions.GetFuturesRsiDailySignalAsync)
        ];

        foreach (var methodName in methodNames)
        {
            typeof(MarketDataAnalyticsQueryExtensions).GetMethods().Should().Contain(method =>
                method.Name == methodName && method.GetParameters().Length == 4);
            typeof(IMarketDataAnalyticsQueryApi).GetMethods().Should().NotContain(method =>
                method.Name == methodName);
        }
    }

    [Fact]
    public async Task TradeSignalIdsUseDirectStorageAndReturnTypedSuccess()
    {
        var (api, db) = CreateApi();
        var valueDate = new DateOnly(2026, 1, 5);
        db.GetFuturesTradeSignalIdByValueDateAsync(valueDate)
            .Returns(Array.Empty<FuturesTradeSignalId>());

        var result = await api.GetFuturesTradeSignalIdsAsync(valueDate);

        api.Should().BeAssignableTo<IFuturesTradeSignalQueryContext>();
        result.Success.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await db.Received(1).GetFuturesTradeSignalIdByValueDateAsync(valueDate);
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("analytics unavailable");
        db.GetFuturesTradeSignalIdByValueDateAsync(Arg.Any<DateOnly>())
            .Returns(_ => Task.FromException<ICollection<FuturesTradeSignalId>>(exception));

        var result = await api.GetFuturesTradeSignalIdsAsync(new DateOnly(2026, 1, 5));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetFuturesTradeSignalIdsQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    [Fact]
    public async Task DailySignalQueriesUseDirectStorageAndReturnTypedSuccessResults()
    {
        var (api, db) = CreateApi();
        var adx = new FuturesAdxSignalReadModel();
        var atr = new FuturesAtrSignalReadModel();
        var macd = new FuturesMacdSignalReadModel();
        var rsi = new FuturesRsiSignalReadModel();
        db.GetLastFuturesAdxDailySignalAsync(ContractId, TimePeriod, PeriodLength).Returns(adx);
        db.GetLastFuturesAtrDailySignalAsync(ContractId, TimePeriod, PeriodLength).Returns(atr);
        db.GetLastFuturesMacdDailySignalAsync(ContractId, TimePeriod, PeriodLength).Returns(macd);
        db.GetLastFuturesRsiDailySignalAsync(ContractId, TimePeriod, PeriodLength).Returns(rsi);

        var adxResult = await api.GetFuturesAdxDailySignalAsync(ContractId, TimePeriod, PeriodLength);
        var atrResult = await api.GetFuturesAtrDailySignalAsync(ContractId, TimePeriod, PeriodLength);
        var macdResult = await api.GetFuturesMacdDailySignalAsync(ContractId, TimePeriod, PeriodLength);
        var rsiResult = await api.GetFuturesRsiDailySignalAsync(ContractId, TimePeriod, PeriodLength);

        adxResult.Should().BeOfType<ServiceOk<FuturesAdxSignalReadModel>>();
        adxResult.Value.Should().BeSameAs(adx);
        atrResult.Should().BeOfType<ServiceOk<FuturesAtrSignalReadModel>>();
        atrResult.Value.Should().BeSameAs(atr);
        macdResult.Should().BeOfType<ServiceOk<FuturesMacdSignalReadModel>>();
        macdResult.Value.Should().BeSameAs(macd);
        rsiResult.Should().BeOfType<ServiceOk<FuturesRsiSignalReadModel>>();
        rsiResult.Value.Should().BeSameAs(rsi);
    }

    [Fact]
    public async Task DailySignalFailuresReturnTheirCorrespondingQueryErrorIds()
    {
        var exception = new InvalidOperationException("daily analytics unavailable");
        var cases = new (Action<IMarketDataDbContext> Arrange,
            Func<IFuturesTradeSignalQueryContext, Task<ServiceResult>> Act,
            int ErrorId)[]
        {
            (db => db.GetLastFuturesAdxDailySignalAsync(ContractId, TimePeriod, PeriodLength)
                    .Returns(_ => Task.FromException<FuturesAdxSignalReadModel?>(exception)),
                async api => await api.GetFuturesAdxDailySignalAsync(ContractId, TimePeriod, PeriodLength),
                GetFuturesAdxDailySignalQuery.ErrorId),
            (db => db.GetLastFuturesAtrDailySignalAsync(ContractId, TimePeriod, PeriodLength)
                    .Returns(_ => Task.FromException<FuturesAtrSignalReadModel?>(exception)),
                async api => await api.GetFuturesAtrDailySignalAsync(ContractId, TimePeriod, PeriodLength),
                GetFuturesAtrDailySignalQuery.ErrorId),
            (db => db.GetLastFuturesMacdDailySignalAsync(ContractId, TimePeriod, PeriodLength)
                    .Returns(_ => Task.FromException<FuturesMacdSignalReadModel?>(exception)),
                async api => await api.GetFuturesMacdDailySignalAsync(ContractId, TimePeriod, PeriodLength),
                GetFuturesMacdDailySignalQuery.ErrorId),
            (db => db.GetLastFuturesRsiDailySignalAsync(ContractId, TimePeriod, PeriodLength)
                    .Returns(_ => Task.FromException<FuturesRsiSignalReadModel?>(exception)),
                async api => await api.GetFuturesRsiDailySignalAsync(ContractId, TimePeriod, PeriodLength),
                GetFuturesRsiDailySignalQuery.ErrorId)
        };

        foreach (var (arrange, act, errorId) in cases)
        {
            var (api, db) = CreateApi();
            arrange(db);

            var result = await act(api);

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(errorId);
            result.ErrorMessage.Should().Be(exception.Message);
        }
    }

    [Fact]
    public async Task ItiSignalDataStartsAllIndependentReadsBeforeAwaitingCompletion()
    {
        var (api, db) = CreateApi();
        var valueDate = new DateOnly(2026, 1, 5);
        var direction = new TaskCompletionSource<FuturesItiSignalV2ReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var extreme = new TaskCompletionSource<FuturesItiSignalV2ReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reversal = new TaskCompletionSource<FuturesItiSignalV2ReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        db.GetLastFuturesItiSignalTrendDirectionChangeAsync(ContractId, valueDate)
            .Returns(_ => Started(direction.Task));
        db.GetLastFuturesItiSignalTrendExtremeChangeAsync(ContractId, valueDate)
            .Returns(_ => Started(extreme.Task));
        db.GetLastFuturesItiSignalTrendReversalChangeAsync(ContractId, valueDate)
            .Returns(_ => Started(reversal.Task));

        var pending = api.GetFuturesItiSignalDataAsync(ContractId, valueDate, TimePeriod);
        await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        pending.IsCompleted.Should().BeFalse();

        direction.SetResult(null);
        extreme.SetResult(null);
        reversal.SetResult(null);
        (await pending).Success.Should().BeTrue();

        Task<FuturesItiSignalV2ReadModel?> Started(Task<FuturesItiSignalV2ReadModel?> task)
        {
            if (Interlocked.Increment(ref started) == 3)
                allStarted.TrySetResult();
            return task;
        }
    }

    [Fact]
    public async Task ItiMdiByTrendStartsBothTrendReadsBeforeAwaitingCompletion()
    {
        var (api, db) = CreateApi();
        var valueDate = new DateOnly(2026, 1, 5);
        var up = new TaskCompletionSource<ICollection<FuturesItiSignalMDIV2ReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var down = new TaskCompletionSource<ICollection<FuturesItiSignalMDIV2ReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        db.GetFuturesItiSignalMDIByTrendAsync(ContractId, valueDate, IntrinsicTimeTrendType.UpTrend, 7)
            .Returns(_ => Started(up.Task));
        db.GetFuturesItiSignalMDIByTrendAsync(ContractId, valueDate, IntrinsicTimeTrendType.DownTrend, 7)
            .Returns(_ => Started(down.Task));

        var pending = api.GetFuturesItiSignalMDIByTrendAsync(ContractId, valueDate, 7);
        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        pending.IsCompleted.Should().BeFalse();

        up.SetResult(Array.Empty<FuturesItiSignalMDIV2ReadModel>());
        down.SetResult(Array.Empty<FuturesItiSignalMDIV2ReadModel>());
        var result = await pending;
        result.Success.Should().BeTrue();
        result.Value.Should().BeEmpty();

        Task<ICollection<FuturesItiSignalMDIV2ReadModel>> Started(
            Task<ICollection<FuturesItiSignalMDIV2ReadModel>> task)
        {
            if (Interlocked.Increment(ref started) == 2)
                bothStarted.TrySetResult();
            return task;
        }
    }

    [Fact]
    public async Task CancellationAwareItiMdiByTrendStartsBothTokenAwareReads()
    {
        var (api, db) = CreateApi();
        var valueDate = new DateOnly(2026, 1, 5);
        using var cancellation = new CancellationTokenSource();
        var up = new TaskCompletionSource<ICollection<FuturesItiSignalMDIV2ReadModel>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var down = new TaskCompletionSource<ICollection<FuturesItiSignalMDIV2ReadModel>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        db.GetFuturesItiSignalMDIByTrendAsync(
                ContractId,
                valueDate,
                IntrinsicTimeTrendType.UpTrend,
                7,
                cancellation.Token)
            .Returns(_ => Started(up.Task));
        db.GetFuturesItiSignalMDIByTrendAsync(
                ContractId,
                valueDate,
                IntrinsicTimeTrendType.DownTrend,
                7,
                cancellation.Token)
            .Returns(_ => Started(down.Task));

        var pending = api.GetFuturesItiSignalMDIByTrendAsync(
            ContractId, valueDate, 7, cancellation.Token);
        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        pending.IsCompleted.Should().BeFalse();

        up.SetResult(Array.Empty<FuturesItiSignalMDIV2ReadModel>());
        down.SetResult(Array.Empty<FuturesItiSignalMDIV2ReadModel>());
        (await pending).Success.Should().BeTrue();

        Task<ICollection<FuturesItiSignalMDIV2ReadModel>> Started(
            Task<ICollection<FuturesItiSignalMDIV2ReadModel>> task)
        {
            if (Interlocked.Increment(ref started) == 2)
                bothStarted.TrySetResult();
            return task;
        }
    }

    static (IFuturesTradeSignalQueryContext Api, IMarketDataDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        var context = Substitute.For<IFuturesTradeSignalQueryContext>();
        context.DbFactory.Returns(dbFactory);
        return (context, db);
    }
}
