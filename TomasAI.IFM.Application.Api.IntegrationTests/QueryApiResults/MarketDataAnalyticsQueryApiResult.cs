using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class MarketDataAnalyticsQueryApiResult
{
    public static Task FromGetFuturesTradeSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesTradeSignalV2ReadModel(
            "ESU25", new DateOnly(2025, 9, 10), TimeFrameType.FifteenSeconds, 1, new TimeOnly(14, 30), 100.0, 2.0, 4500.0, 0.5, 1.2, 60.0, 0.3,
            FuturesTrendType.UpTrend, FuturesTrendStrengthType.High, TradeSignalType.Buy, FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.High,
            1.1, FuturesMDITrendType.UpTrending, 1.2, 1.0, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 200m, 400m, TradeExecuteState.InTrade));

    public static Task FromGetLastFuturesTradeSignalAsync(HttpResponse resp)
        => FromGetFuturesTradeSignalAsync(resp);

    public static Task FromGetFuturesTradeSignalBySymbolAsync(HttpResponse resp)
        => FromGetFuturesTradeSignalAsync(resp);

    public static Task FromGetFuturesTradeSignalIdsAsync(HttpResponse resp)
        => resp.SetResult(new[] { new FuturesTradeSignalId("ESU25", new DateOnly(2025, 9, 10), TimeFrameType.FifteenSeconds, 1) });

    public static Task FromGetFuturesRsiSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesRsiSignalReadModel(
            "ESU25", new DateOnly(2025, 9, 10), TimeFrameType.OneMinute, 14, new TimeOnly(14, 30), 4500m, 0.5m, 0.5m, 0.2m, 0.3m, 1.1m, 60.0, 59.0, 0.3, 30));

    public static Task FromGetFuturesRsiSignalWithTypeAsync(HttpResponse resp)
        => FromGetFuturesRsiSignalAsync(resp);

    public static Task FromGetFuturesTrendDirectionFromRSISignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesTrendDirectionReadModel(
            "ESU25", new DateOnly(2025, 9, 10), new TimeOnly(14, 30), 30, 10, 5, FuturesTrendType.UpTrend));

    public static Task FromGetFuturesTdiSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesTdiSignalReadModel(
            "ESU25", new DateOnly(2025, 9, 10), TimeFrameType.FifteenSeconds, new TimeOnly(14, 30), 10, 5, FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.High));

    public static Task FromGetFuturesItiSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesItiSignalV2ReadModel(
            contractId: "SYM20251215",
            valueDate: new DateOnly(2025, 1, 15),
            timePeriod: TimeFrameType.Weekly,
            sequenceId: 1,
            intrinsicTime: DateTime.Now,
            intrinsicTimeGroupId: 1,
            intrinsicTimeLength: 1,
            intrinsicPrice: 1,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: 1,
            trendExtreme: 1,
            trendReversal: 1,
            lambda: 1,
            tradingDays: 0,
            threshold: 0,
            targetDelta: 1,
            trendDelta: 1,
            upTrendTrigger: 1,
            downTrendTrigger: 1,
            tradeState: IntrinsicTimeTradeState.Ready
    ));

    public static Task FromGetFuturesItiTrendDirectionChangedSignalsAsync(HttpResponse resp)
        => resp.SetResult(new[] { new FuturesItiSignalV2ReadModel(
        contractId: "SYM20251215",
        valueDate: new DateOnly(2025, 1, 15),
        timePeriod: TimeFrameType.Weekly,
        sequenceId: 1,
        intrinsicTime: DateTime.Now,
        intrinsicTimeGroupId: 1,
        intrinsicTimeLength: 1,
        intrinsicPrice: 1,
        intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
        intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
        trendPrice: 1,
        trendExtreme: 1,
        trendReversal: 1,
        lambda: 1,
        tradingDays: 0,
        threshold: 0,
        targetDelta: 1,
        trendDelta: 1,
        upTrendTrigger: 1,
        downTrendTrigger: 1,
        tradeState: IntrinsicTimeTradeState.Ready
    ) });

    public static Task FromGetFuturesItiSignalDataAsync(HttpResponse resp)
        => resp.SetResult(new FuturesItiSignalDataReadModel(
            new FuturesItiSignalV2ReadModel(
        contractId: "SYM20251215",
        valueDate: new DateOnly(2025, 1, 15),
        timePeriod: TimeFrameType.Weekly,
        sequenceId: 1,
        intrinsicTime: DateTime.Now,
        intrinsicTimeGroupId: 1,
        intrinsicTimeLength: 1,
        intrinsicPrice: 1,
        intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
        intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
        trendPrice: 1,
        trendExtreme: 1,
        trendReversal: 1,
        lambda: 1,
        tradingDays: 0,
        threshold: 0,
        targetDelta: 1,
        trendDelta: 1,
        upTrendTrigger: 1,
        downTrendTrigger: 1,
        tradeState: IntrinsicTimeTradeState.Ready
    ),
    null,
    null));

    public static Task FromGetFuturesItiMDIDistributionAsync(HttpResponse resp)
        => resp.SetResult(new FuturesItiMDIDistributionReadModel());

    public static Task FromGetFuturesItiMDIDistributionByTrendAsync(HttpResponse resp)
        => FromGetFuturesItiMDIDistributionAsync(resp);

    public static Task FromGetFuturesItiSignalMDIAsync(HttpResponse resp)
        => resp.SetResult(new[] { new FuturesItiSignalMDIV2ReadModel("ESU25", new DateOnly(2025, 9, 10), new DateTime(2025, 9, 10, 14, 30, 0), IntrinsicTimeTrendType.UpTrend, 1.0) });

    public static Task FromGetFuturesItiSignalMDIByTrendAsync(HttpResponse resp)
        => FromGetFuturesItiSignalMDIAsync(resp);

    public static Task FromGetFuturesAtrSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesAtrSignalReadModel(
            "ESU25", new DateOnly(2025, 9, 10), TimeFrameType.FifteenSeconds, 14,
            new TimeOnly(14, 30), 5500m, 1.5, 2.0, FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.High));

    public static Task FromGetFuturesAdxSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesAdxSignalReadModel(
            "ESU25", new DateOnly(2025, 9, 10), TimeFrameType.FifteenSeconds, 14, new TimeOnly(14, 30), 5500,
            25.0, 15.0, 30.0, FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.High));

    public static Task FromGetFuturesMacdSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesMacdSignalReadModel(
            contractId: "ESU25",
            valueDate: new DateOnly(2025, 9, 10),
            timePeriod: TimeFrameType.FifteenSeconds,
            signalEmaPeriod: 7,
            fastEmaPeriod: 10,
            slowEmaPeriod: 30,
            timestamp: new TimeOnly(14, 30),
            futuresPrice: 5500m,
            macdLine: 1.5,
            signalLine: 1.2,
            histogram: 0.3,
            macd: FuturesTrendDirectionType.UpTrending,
            macdStrength: FuturesTrendDirectionStrengthType.High,
            fastEma: 5501.5,
            slowEma: 5500.0));
}
