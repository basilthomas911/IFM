using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class MarketDataFeedQueryApiResult
{
    public static Task FromGetLastFuturesTickDataAsync(HttpResponse resp)
        => resp.SetResult(new FuturesTickDataV2ReadModel("ES20251010", new System.DateOnly(2025, 10, 10), 1, new System.TimeOnly(9, 30), 4500.25m, 10));

    public static Task FromGetLastFuturesTickDataByTickDateAsync(HttpResponse resp)
        => resp.SetResult(new FuturesTickDataV2ReadModel("ES20251010", new System.DateOnly(2025, 10, 10), 2, new System.TimeOnly(9, 31), 4501.75m, 5));

    public static Task FromGetLastFuturesOptionTickDataAsync(HttpResponse resp)
        => resp.SetResult(new FuturesOptionTickDataV2ReadModel(
            "ES20251010C4500", new System.DateOnly(2025, 10, 10), 10, new System.TimeOnly(9, 32),
            optionPrice: 50.5, bidPrice: 50.0, askPrice: 51.0, bidSize: 5, askSize: 5, impliedVolatility: 0.25, underlyingPrice: 4500.25,
            delta: 0.45, gamma: 0.01, vega: 0.12, theta: -0.02, rho: 0.001));

    public static Task FromGetFuturesEodDataAsync(HttpResponse resp)
        => resp.SetResult(new FuturesEodDataV2ReadModel(
            contractId: "ES20251010",
            valueDate: new System.DateOnly(2025, 10, 10),
            symbol: "ES",
            openPrice: 4480.00m,
            highPrice: 4510.00m,
            lowPrice: 4475.00m,
            closePrice: 4500.25m,
            volume: 12500,
            dailyPercentChange: 0.005,
            dailyStdDev: 0.8,
            dailyStdDevAmount: 36.0,
            upperBand: 4550.0,
            mean: 4500.0,
            lowerBand: 4450.0,
            marketDirection: MarketDirectionType.Up,
            marketVolatility: MarketVolatilityType.Rising,
            priceDirection: PriceDirectionType.Rising,
            priceVolatility: PriceVolatilityType.Rising,
            marketDirectionIndicator: 65.0,
            windowSize: 20
        ));

    public static Task FromGetLastFuturesEodDataAsync(HttpResponse resp)
        => resp.SetResult(new FuturesEodDataV2ReadModel(
            "ES20251010", new System.DateOnly(2025, 10, 10), "ES", 4480.00m, 4510.00m, 4475.00m, 4500.25m, 12500,
            dailyPercentChange: 0.005, dailyStdDev: 0.8, dailyStdDevAmount: 36.0, upperBand: 4550.0, mean: 4500.0, lowerBand: 4450.0,
            marketDirection: MarketDirectionType.Up, marketVolatility: MarketVolatilityType.Rising, priceDirection: PriceDirectionType.Rising,
            priceVolatility: PriceVolatilityType.Rising, marketDirectionIndicator: 65.0, windowSize: 20));

    public static Task FromGetFuturesEodDataByDateRangeAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new FuturesEodDataV2ReadModel("ES20251010", new System.DateOnly(2025,10,9), "ES", 4475.00m, 4490.00m, 4465.00m, 4485.00m, 11000),
            new FuturesEodDataV2ReadModel("ES20251010", new System.DateOnly(2025,10,10), "ES", 4480.00m, 4510.00m, 4475.00m, 4500.25m, 12500)
        });

    public static Task FromGetFuturesBarDataAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new FuturesBarDataReadModel("ES20251010", "ES", new System.DateOnly(2025,10,10), DateTime.SpecifyKind(new System.DateTime(2025,10,10,9,30,0), DateTimeKind.Utc), BarRateType.Minute, 4500.25m, 0.5, -0.5),
            new FuturesBarDataReadModel("ES20251010", "ES", new System.DateOnly(2025,10,10), DateTime.SpecifyKind(new System.DateTime(2025,10,10,9,31,0), DateTimeKind.Utc), BarRateType.Minute, 4501.00m, 0.6, -0.4)
        });

    public static Task FromGetLastFuturesBarDataAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new FuturesBarDataReadModel("ES20251010", "ES", new System.DateOnly(2025,10,10), DateTime.SpecifyKind(new System.DateTime(2025,10,10,9,31,0), DateTimeKind.Utc), BarRateType.Minute, 4501.00m, 0.6, -0.4)
        });

    public static Task FromGetIronCondorMarketDataFeedAsync(HttpResponse resp)
        => resp.SetResult(new IronCondorMarketDataFeedReadModel(
            assetPrice: 4500.25m,
            shortPutOptionData: new FuturesOptionTickDataV2ReadModel("ES20251010P4450", new System.DateOnly(2025,10,10), 1, new System.TimeOnly(9,30), 50.0, 49.5, 50.5, 2, 2, 0.20, 4500.25, 0.5, 0.01, 0.1, -0.01, 0.0),
            longPutOptionData: new FuturesOptionTickDataV2ReadModel("ES20251010P4400", new System.DateOnly(2025,10,10), 2, new System.TimeOnly(9,30), 25.0, 24.5, 25.5, 1, 1, 0.22, 4500.25, 0.25, 0.005, 0.05, -0.005, 0.0),
            shortCallOptionData: new FuturesOptionTickDataV2ReadModel("ES20251010C4550", new System.DateOnly(2025,10,10), 3, new System.TimeOnly(9,30), 55.0, 54.5, 55.5, 2, 2, 0.27, 4500.25, 0.55, 0.02, 0.13, -0.02, 0.0),
            longCallOptionData: new FuturesOptionTickDataV2ReadModel("ES20251010C4600", new System.DateOnly(2025,10,10), 4, new System.TimeOnly(9,30), 30.0, 29.5, 30.5, 1, 1, 0.24, 4500.25, 0.3, 0.008, 0.06, -0.006, 0.0)
        ));

    public static Task FromGetFuturesEodDataParametersAsync(HttpResponse resp)
        => resp.SetResult(new FuturesEodDataParametersReadModel(
            new FuturesEodDataV2ReadModel("ES20251010", new System.DateOnly(2025,10,10), "ES", 4480.00m, 4510.00m, 4475.00m, 4500.25m, 12500),
            new[] { new FuturesEodDataV2ReadModel("ES20251010", new System.DateOnly(2025,10,9), "ES", 4475.00m, 4490.00m, 4465.00m, 4485.00m, 11000) },
            new NormalCurveTableReadModel(new[] { new NormalCurveDataReadModel(0.0, 0.01), new NormalCurveDataReadModel(1.0, 0.02), new NormalCurveDataReadModel(2.0, 0.03) })
        ));

    public static Task FromGetFuturesOptionContractAsync(HttpResponse resp)
        => resp.SetResult(new FuturesOptionContractReadModel(
            contractId: "ES20251010C4500",
            description: "ES Oct 10 2025 Call 4500",
            symbol: "ES",
            localSymbol: "ES 20251010 C4500",
            securityType: "OPT",
            currency: "USD",
            exchange: "GLOBEX",
            multiplier: "50",
            contractMonth: new System.DateOnly(2025,10,10),
            strikePrice: 4500,
            optionType: "CALL"
        ));

    public static Task FromGetFuturesOptionSpreadDataAsync(HttpResponse resp)
        => resp.SetResult(new FuturesOptionSpreadDataReadModel(
            new FuturesOptionDataReadModel(49.5, 50.5, 0.20, 0.45, 0.01, -0.02),
            new FuturesOptionDataReadModel(24.5, 25.5, 0.22, 0.25, 0.005, -0.005)
        ));

    public static Task FromGetNormalCurveTableAsync(HttpResponse resp)
        => resp.SetResult(new NormalCurveTableReadModel(new[] {
            new NormalCurveDataReadModel(0.0, 0.01),
            new NormalCurveDataReadModel(0.5, 0.02),
            new NormalCurveDataReadModel(1.0, 0.03)
        }));

    public static Task FromGetVixFuturesEodDataAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new VixFuturesEodDataReadModel("VIX20251010", new System.DateOnly(2025,10,10), 12.5m, 13.0m, 12.0m, 12.8m, 500),
            new VixFuturesEodDataReadModel("VIX20251009", new System.DateOnly(2025,10,9), 12.0m, 12.6m, 11.8m, 12.1m, 300)
        });

    public static Task FromGetLastVixFuturesEodDataAsync(HttpResponse resp)
        => resp.SetResult(new VixFuturesEodDataReadModel("VIX20251010", new System.DateOnly(2025,10,10), 12.5m, 13.0m, 12.0m, 12.8m, 500));

    public static Task FromGetFuturesRiskPositionTypeAsync(HttpResponse resp)
        => resp.SetResult(new RiskPositionTypeReadModel(RiskPositionType.Medium));

    public static Task FromGetFuturesEodMovingAveragesAsync(HttpResponse resp)
        => resp.SetResult(new FuturesEodDataMovingAveragesReadModel("ES", new System.DateOnly(2025,10,10), 4450.0m, 4200.0m));

    public static Task FromGetStreamingRequestIdAsync(HttpResponse resp)
        => resp.SetResult(new ScalarValue<int>(12345));

    public static Task FromGetOptionQuoteIdAsync(HttpResponse resp)
        => resp.SetResult(new ScalarValue<int>(67890));
}
