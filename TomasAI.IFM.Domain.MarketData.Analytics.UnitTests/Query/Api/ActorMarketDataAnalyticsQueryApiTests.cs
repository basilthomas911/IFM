using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Query.Api;
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
    public void DailySignalQueriesAreActorOnlyContractMethods()
    {
        string[] methodNames =
        [
            nameof(IActorMarketDataAnalyticsQueryApi.GetFuturesAdxDailySignalAsync),
            nameof(IActorMarketDataAnalyticsQueryApi.GetFuturesAtrDailySignalAsync),
            nameof(IActorMarketDataAnalyticsQueryApi.GetFuturesMacdDailySignalAsync),
            nameof(IActorMarketDataAnalyticsQueryApi.GetFuturesRsiDailySignalAsync)
        ];

        foreach (var methodName in methodNames)
        {
            typeof(IActorMarketDataAnalyticsQueryApi).GetMethod(methodName).Should().NotBeNull();
            typeof(IMarketDataAnalyticsQueryApi).GetMethod(methodName).Should().BeNull();
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

        api.Should().BeAssignableTo<IActorMarketDataAnalyticsQueryApi>();
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
            Func<ActorMarketDataAnalyticsQueryApi, Task<ServiceResult>> Act,
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

    static (ActorMarketDataAnalyticsQueryApi Api, IMarketDataDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        dbFactory.MarketDataDb.Returns(db);
        return (new ActorMarketDataAnalyticsQueryApi(dbFactory), db);
    }
}
