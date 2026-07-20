using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class MarketDataQueryApiResult
{
    public static Task FromGetCurrentlyTradedFuturesContractAsync(HttpResponse resp)
        => resp.SetResult(new FuturesContractV2ReadModel(
            "ES20251010", "ES", "ES", "FUT", "USD", "GLOBEX", "50", "Active", new System.DateOnly(2025, 10, 10), true));

    public static Task FromGetCurrentlyTradedFuturesContractsAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new FuturesContractV2ReadModel("ES20251010", "ES", "ES", "FUT", "USD", "GLOBEX", "50", "Active", new System.DateOnly(2025, 10, 10), true),
            new FuturesContractV2ReadModel("ES20251011", "ES", "ES", "FUT", "USD", "GLOBEX", "50", "Active", new System.DateOnly(2025, 10, 11), true)
        });

    public static Task FromGetFuturesContractAsync(HttpResponse resp)
        => resp.SetResult(new FuturesContractV2ReadModel(
            "ES20251010", "ES", "ES", "FUT", "USD", "GLOBEX", "50", "Active", new System.DateOnly(2025, 10, 10), true));

    public static Task FromGetFuturesContractSymbolAsync(HttpResponse resp)
        => resp.SetResult("ES");

    public static Task FromGetFuturesTradeSignalAsync(HttpResponse resp)
        => resp.SetResult(new FuturesTradeSignalV2ReadModel(
            "ES20251010",
            new System.DateOnly(2025, 10, 10),
            TradeTimePeriodType.FifteenSeconds,
            1L,
            new System.TimeOnly(9, 30),
            100.0, 2.0, 101.0, 0.5, 0.1, 55.0, 0.2,
            FuturesTrendType.UpTrending,
            FuturesTrendStrengthType.High,
            TradeSignalType.Buy,
            FuturesTrendDirectionType.UpTrending,
            FuturesTrendDirectionStrengthType.High,
            1.5,
            FuturesMDITrendType.UpTrending,
            2.0, 1.0, 1.1, 0.9, 1.2, 0.8, 0.3, 0.7, 0.6,
            4500.0m, 4400.0m,
            TradeExecuteState.Yes
        ));

    public static Task FromGetFuturesOptionContractAsync(HttpResponse resp)
        => resp.SetResult(new FuturesOptionContractReadModel(
            "ES20251010C4500", "OptionDesc", "ES", "ESOPT", "OPT", "USD", "GLOBEX", "50", new System.DateOnly(2025, 10, 10), 4500, "CALL"));

    public static Task FromGetFuturesContractsAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new FuturesContractV2ReadModel("ES20251010", "ES", "ES", "FUT", "USD", "GLOBEX", "50", "Active", new System.DateOnly(2025, 10, 10), true),
            new FuturesContractV2ReadModel("ES20251011", "ES", "ES", "FUT", "USD", "GLOBEX", "50", "Active", new System.DateOnly(2025, 10, 11), true)
        });

    public static Task FromGetFuturesOptionContractsAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new FuturesOptionContractReadModel("ES20251010C4500", "OptionDesc", "ES", "ESOPT", "OPT", "USD", "GLOBEX", "50", new System.DateOnly(2025, 10, 10), 4500, "CALL"),
            new FuturesOptionContractReadModel("ES20251010C4600", "OptionDesc", "ES", "ESOPT", "OPT", "USD", "GLOBEX", "50", new System.DateOnly(2025, 10, 10), 4600, "CALL")
        });

    public static Task FromGetLastYieldCurveRateAsync(HttpResponse resp)
        => resp.SetResult(new YieldCurveRateReadModel(
            new System.DateOnly(2025, 10, 10), 0.01, 0.011, 0.012, 0.013, 0.014, 0.015, 0.016, 0.017, 0.018, 0.019, 0.02, 0.021));

    public static Task FromGetLastRateOfReturnAsync(HttpResponse resp)
        => resp.SetResult(new RateOfReturnReadModel("ES", new System.DateOnly(2025, 10, 10), 0.05));

    public static Task FromGetTradingDaysAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<int>(252));

    public static Task FromGetTradingDatesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new System.DateOnly(2025, 10, 10),
            new System.DateOnly(2025, 10, 13)
        });

    public static Task FromGetYieldCurveRatesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new YieldCurveRateReadModel(new System.DateOnly(2025, 10, 10), 0.01, 0.011, 0.012, 0.013, 0.014, 0.015, 0.016, 0.017, 0.018, 0.019, 0.02, 0.021),
            new YieldCurveRateReadModel(new System.DateOnly(2025, 10, 11), 0.02, 0.021, 0.022, 0.023, 0.024, 0.025, 0.026, 0.027, 0.028, 0.029, 0.03, 0.031)
        });

    public static Task FromGetYieldCurveRateYearsAsync(HttpResponse resp)
        => resp.SetResult(new YieldCurveRateYearsReadModel(new[] { 2025, 2026 }));

    public static Task FromYieldCurveRateExistsAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<bool>(true));

    public static Task FromGetValueDateAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<System.DateOnly>(new System.DateOnly(2025, 10, 10)));

    public static Task FromGetExternalYieldCurveRatesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new YieldCurveRateReadModel(new System.DateOnly(2025, 10, 10), 0.01, 0.011, 0.012, 0.013, 0.014, 0.015, 0.016, 0.017, 0.018, 0.019, 0.02, 0.021)
        });

    public static Task FromGetIronCondorMarketDataAsync(HttpResponse resp)
        => resp.SetResult(new IronCondorMarketDataReadModel(
            new FuturesContractV2ReadModel("ES20251010", "ES", "ES", "FUT", "USD", "GLOBEX", "50", "Active", new System.DateOnly(2025, 10, 10), true),
            new FuturesOptionContractReadModel("ES20251010C4500", "OptionDesc", "ES", "ESOPT", "OPT", "USD", "GLOBEX", "50", new System.DateOnly(2025, 10, 10), 4500, "CALL"),
            new FuturesOptionContractReadModel("ES20251010C4600", "OptionDesc", "ES", "ESOPT", "OPT", "USD", "GLOBEX", "50", new System.DateOnly(2025, 10, 10), 4600, "CALL"),
            new FuturesOptionContractReadModel("ES20251010C4700", "OptionDesc", "ES", "ESOPT", "OPT", "USD", "GLOBEX", "50", new System.DateOnly(2025, 10, 10), 4700, "CALL"),
            new FuturesOptionContractReadModel("ES20251010C4800", "OptionDesc", "ES", "ESOPT", "OPT", "USD", "GLOBEX", "50", new System.DateOnly(2025, 10, 10), 4800, "CALL"),
            0.01,
            252
        ));

    public static Task FromGetFuturesOptionContractIdsAsync(HttpResponse resp)
        => resp.SetResult(new[] { "ES20251010C4500", "ES20251010C4600" });
}
