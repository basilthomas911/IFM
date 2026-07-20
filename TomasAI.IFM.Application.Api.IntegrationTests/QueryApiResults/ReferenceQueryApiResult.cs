using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

public static class ReferenceQueryApiResult
{
    public static Task FromGetDefaultFuturesContractDefinitionsAsync(HttpResponse resp)
        => resp.SetResult(new DefaultFuturesContractDefinitionsReadModel
        {
            Currency = "USD",
            Exchange = "CME",
            Multiplier = "50",
            SecurityType = "FUT",
            OptionSecurityType = "OPT",
            Symbol = "ES"
        });

    public static Task FromGetLookupTypesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new LookupTypeReadModel("Currency", "USD", 1, "US Dollar", System.DateTime.UtcNow, "tester"),
            new LookupTypeReadModel("Exchange", "CME", 1, "CME Exchange", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetLookupTypeAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new LookupTypeReadModel("SecurityType", "FUT", 1, "Futures", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetLookupTypeNamesAsync(HttpResponse resp)
        => resp.SetResult(new[] { "Currency", "Exchange", "SecurityType" });

    public static Task FromGetLookupTypeShortCodesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new LookupTypeShortCodeReadModel("USD", 1),
            new LookupTypeShortCodeReadModel("CME", 2)
        });

    public static Task FromGetMarketDataDefinitionTypesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new LookupTypeReadModel("MarketDataDefinitionType", "Futures", 1, "Futures market data", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetReferenceDataDefinitionTypesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new LookupTypeReadModel("ReferenceDataDefinitionType", "StrikePrice", 1, "Strike price definitions", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetSystemAdminFunctionTypesAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new LookupTypeReadModel("SystemAdminFunctionType", "UserMgmt", 1, "User management", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetNextSeedIdAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<int>(1001));

    public static Task FromGetCurrentSeedIdAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<int>(1000));

    public static Task FromGetFuturesOptionStrikePriceDefinitionsAsync(HttpResponse resp)
        => resp.SetResult(new FuturesOptionStrikePriceReadModel { Minimum = 5, Maximum = 10000, Increment = 5 });

    public static Task FromLookupTypeShortCodeExistsAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<bool>(true));

    public static Task FromGetEconomicCalendarsAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new EconomicCalendarReadModel(System.DateTime.UtcNow.Date, "US", "NFP", "1000", "950", "900", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetEconomicCalendarAllAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new EconomicCalendarReadModel(System.DateTime.UtcNow.Date, "US", "NFP", "1000", "950", "900", System.DateTime.UtcNow, "tester")
        });

    public static Task FromGetExternalEconomicCalendarsAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new EconomicCalendarReadModel(System.DateTime.UtcNow.Date, "US", "NFP", "1000", "950", "900", System.DateTime.UtcNow, "external")
        });

    public static Task FromGetEconomicCalendarDateAsync(HttpResponse resp)
        => resp.SetResult(System.DateTime.UtcNow.Date.ToString("yyyy-MM-dd"));

    public static Task FromGetEconomicCalendarCountryCodesAsync(HttpResponse resp)
        => resp.SetResult(new[] { new EconomicCalendarCountryCodeReadModel("US"), new EconomicCalendarCountryCodeReadModel("GB") });

    public static Task FromGetMDIForwardLossRatiosAsync(HttpResponse resp)
        => resp.SetResult(new[] {
            new MDIForwardLossRatioReadModel(100, IntrinsicTimeTrendType.UpTrend, TradeType.ShortIronCondor, 0.75, "tester", System.DateTime.UtcNow, "tester", System.DateTime.UtcNow)
        });
}
