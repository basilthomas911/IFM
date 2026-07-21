using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.OptionPricer.QueryParameters;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.QueryParameters;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Actor;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query.Actor;

namespace TomasAI.IFM.Application.Actor.IntegrationTests;

/// <summary>
/// Provides extension methods for mapping API query endpoints to an endpoint route builder.
/// </summary>
public static class MapQueryExtension
{
    public static IEndpointRouteBuilder MapApiQueries(this IEndpointRouteBuilder endpoints)
    {
        // Chain all query mapping methods here
        return endpoints
            .MapFundQueries()
            .MapFundTransactionQueries()
            .MapReferenceQueries()
            .MapMarketDataQueries()
            .MapMarketDataFeedQueries()
            .MapMarketDataAnalyticsQueries()
            .MapOptionPricerQueries()
            .MapTradeQueries()
            .MapOptionTradeQueries()
            .MapSystemAdminQueries();
    }
}

/// <summary>
/// Provides extension methods for mapping fund-related query endpoints to an endpoint route builder.
/// </summary>
/// <remarks>This class contains methods that register HTTP GET endpoints for various fund queries, such as
/// retrieving fund balances, transactions, orders, and reports. These endpoints are intended to be used with minimal
/// API routing in ASP.NET Core applications. All methods are static and designed to be used as extension methods on
/// IEndpointRouteBuilder.</remarks>
public static class FundQueries
{
    public static IEndpointRouteBuilder MapFundQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(FundQueryUriPath.GetClosingFundBalance, async (IActorService e, int fundId, DateOnly valueDate) =>
        {
            var query = new GetClosingFundBalanceQuery(fundId, valueDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetClosingFundBalanceQuery.Actor, GetClosingFundBalanceQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundBalanceReadModel, GetClosingFundBalanceQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetOpeningFundBalance, async (IActorService e, int fundId, DateOnly valueDate) =>
        {
            var query = new GetOpeningFundBalanceQuery(fundId, valueDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetOpeningFundBalanceQuery.Actor, GetOpeningFundBalanceQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundBalanceReadModel, GetOpeningFundBalanceQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFundBalance, async (IActorService e, int fundId) =>
        {
            var query = new GetFundBalanceQuery(fundId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundBalanceQuery.Actor, GetFundBalanceQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundBalanceReadModel, GetFundBalanceQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFunds, async (IActorService e) =>
        {
            var query = new GetFundsQuery();
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundsQuery.Actor, GetFundsQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundReadModel[], GetFundsQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFundOrders, async (IActorService e) =>
        {
            var query = new GetFundOrdersQuery();
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundOrdersQuery.Actor, GetFundOrdersQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundOrderReadModel[], GetFundOrdersQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFundOrderTrades, async (IActorService e) =>
        {
            var query = new GetFundOrderTradesQuery();
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundOrderTradesQuery.Actor, GetFundOrderTradesQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundOrderTradeReadModel[], GetFundOrderTradesQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFundPnlReport, async (IActorService e, int fundId, DateOnly startDate, DateOnly endDate) =>
        {
            var query = new GetFundPnlReportQuery(fundId, startDate, endDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundPnlReportQuery.Actor, GetFundPnlReportQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundPnlReportReadModel, GetFundPnlReportQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFundIdFromOrderId, async (IActorService e, int orderId) =>
        {
            var query = new GetFundIdFromOrderIdQuery(orderId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundIdFromOrderIdQuery.Actor, GetFundIdFromOrderIdQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<ScalarReadModel<int>, GetFundIdFromOrderIdQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFundWinLossRatio, async (IActorService e, int fundId, DateOnly startDate, DateOnly endDate) =>
        {
            var query = new GetFundWinLossRatioQuery(fundId, startDate, endDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundWinLossRatioQuery.Actor, GetFundWinLossRatioQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundWinLossRatioReadModel, GetFundWinLossRatioQuery>(query);
        });

        endpoints.MapGet(FundQueryUriPath.GetFundDrawdownBalances, async (IActorService e, int fundId, DateOnly startDate, DateOnly endDate) =>
        {
            var query = new GetFundDrawdownBalancesQuery(fundId, startDate, endDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundDrawdownBalancesQuery.Actor, GetFundDrawdownBalancesQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundDrawdownBalancesReadModel, GetFundDrawdownBalancesQuery>(query);
        });

        return endpoints;
    }


}

public static class FundTransactionQueries
{
    public static IEndpointRouteBuilder MapFundTransactionQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(FundQueryUriPath.GetFundTransactions, async (IActorService e, int fundId, DateOnly startDate, DateOnly endDate) =>
        {
            var query = new GetFundTransactionsQuery(fundId, startDate, endDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFundTransactionsQuery.Actor, GetFundTransactionsQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<FundTransactionReadModel[], GetFundTransactionsQuery>(query);
        });

        return endpoints;
    }
}

public static class ReferenceQueries
{
    public static IEndpointRouteBuilder MapReferenceQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ReferenceQueryUriPath.GetDefaultFuturesContractDefinitions, async (IActorService e) =>
        {
            var query = new GetDefaultFuturesContractDefinitionsQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetDefaultFuturesContractDefinitionsQuery.Actor, GetDefaultFuturesContractDefinitionsQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<DefaultFuturesContractDefinitionsReadModel, GetDefaultFuturesContractDefinitionsQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetCurrentSeedId, async (IActorService e, string seedType) =>
        {
            var query = new GetCurrentSeedIdQuery(seedType);
            query.Subject = new ActorSubject(ActorType.Query, GetCurrentSeedIdQuery.Actor, GetCurrentSeedIdQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<ScalarReadModel<int>, GetCurrentSeedIdQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetNextSeedId, async (IActorService e, string seedType) =>
        {
            var query = new GetNextSeedIdQuery(seedType);
            query.Subject = new ActorSubject(ActorType.Query, GetNextSeedIdQuery.Actor, GetNextSeedIdQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<ScalarReadModel<int>, GetNextSeedIdQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetFuturesOptionStrikePriceDefinitions, async (IActorService e) =>
        {
            var query = new GetFuturesOptionStrikePriceDefinitionsQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesOptionStrikePriceDefinitionsQuery.Actor, GetFuturesOptionStrikePriceDefinitionsQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesOptionStrikePriceReadModel, GetFuturesOptionStrikePriceDefinitionsQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetMDIForwardLossRatios, async (IActorService e, IntrinsicTimeTrendType trendDirection, TradeType tradeType) =>
        {
            var query = new GetMDIForwardLossRatiosQuery(trendDirection, tradeType);
            query.Subject = new ActorSubject(ActorType.Query, GetMDIForwardLossRatiosQuery.Actor, GetMDIForwardLossRatiosQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<MDIForwardLossRatioReadModel[], GetMDIForwardLossRatiosQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetLookupTypes, async (IActorService e) =>
        {
            var query = new GetLookupTypesQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypesQuery.Actor, GetLookupTypesQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetLookupType, async (IActorService e, string lookupTypeName) =>
        {
            var query = new GetLookupTypeQuery(lookupTypeName);
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<LookupTypeCollection, GetLookupTypeQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetLookupTypeNames, async (IActorService e) =>
        {
            var query = new GetLookupTypeNamesQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypeNamesQuery.Actor, GetLookupTypeNamesQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<string[], GetLookupTypeNamesQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetLookupTypeShortCodes, async (IActorService e, string lookupTypeName) =>
        {
            var query = new GetLookupTypeShortCodesQuery(lookupTypeName);
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypeShortCodesQuery.Actor, GetLookupTypeShortCodesQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<LookupTypeShortCodeReadModel[], GetLookupTypeShortCodesQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.LookupTypeShortCodeExists, async (IActorService e, string lookupTypeName, string shortCode) =>
        {
            var query = new GetLookupTypeShortCodeExistsQuery(lookupTypeName, shortCode);
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypeShortCodeExistsQuery.Actor, GetLookupTypeShortCodeExistsQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<ScalarReadModel<bool>, GetLookupTypeShortCodeExistsQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetMarketDataDefinitionTypes, async (IActorService e) =>
        {
            var query = new GetLookupTypeQuery("MarketDataDefinitionType");
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<LookupTypeCollection, GetLookupTypeQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetReferenceDataDefinitionTypes, async (IActorService e) =>
        {
            var query = new GetLookupTypeQuery("ReferenceDataDefinitionType");
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<LookupTypeCollection, GetLookupTypeQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetSystemAdminFunctionTypes, async (IActorService e) =>
        {
            var query = new GetLookupTypeQuery("SystemAdminFunctionType");
            query.Subject = new ActorSubject(ActorType.Query, GetLookupTypeQuery.Actor, GetLookupTypeQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<LookupTypeCollection, GetLookupTypeQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetEconomicCalendars, async (IActorService e, EconomicCalendarViewType calendarViewType, string countryCode, DateTime todaysDate) =>
        {
            var query = new GetEconomicCalendarQuery(todaysDate, calendarViewType, countryCode);
            query.Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarQuery.Actor, GetEconomicCalendarQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<EconomicCalendarReadModel[], GetEconomicCalendarQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetEconomicCalendarAll, async (IActorService e) =>
        {
            var query = new GetEconomicCalendarAllQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarAllQuery.Actor, GetEconomicCalendarAllQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<EconomicCalendarReadModel[], GetEconomicCalendarAllQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetExternalEconomicCalendars, async (IActorService e) =>
        {
            var query = new GetExternalEconomicCalendarsQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetExternalEconomicCalendarsQuery.Actor, GetExternalEconomicCalendarsQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<EconomicCalendarReadModel[], GetExternalEconomicCalendarsQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetEconomicCalendarDate, async (IActorService e, DateTime todaysDate, EconomicCalendarViewType calendarViewType) =>
        {
            var query = new GetEconomicCalendarDateQuery(todaysDate, calendarViewType);
            query.Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarDateQuery.Actor, GetEconomicCalendarDateQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<string, GetEconomicCalendarDateQuery>(query);
        });

        endpoints.MapGet(ReferenceQueryUriPath.GetEconomicCalendarCountryCodes, async (IActorService e) =>
        {
            var query = new GetEconomicCalendarCountryCodesQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetEconomicCalendarCountryCodesQuery.Actor, GetEconomicCalendarCountryCodesQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<EconomicCalendarCountryCodeReadModel[], GetEconomicCalendarCountryCodesQuery>(query);
        });

        return endpoints;
    }
}

public static class MarketDataQueries
{
    public static IEndpointRouteBuilder MapMarketDataQueries(this IEndpointRouteBuilder endpoints)
    {
        // FuturesContractQueryActor queries
        endpoints.MapGet(MarketDataQueryUriPath.GetCurrentlyTradedFuturesContract, async (IActorService e, string symbol) =>
        {
            var entityId = new GetCurrentlyTradedFuturesContractParameter(symbol);
            GetCurrentlyTradedFuturesContractQuery query = new GetCurrentlyTradedFuturesContractQuery(symbol)
            {
                Subject = new ActorSubject(ActorType.Query, GetCurrentlyTradedFuturesContractQuery.Actor, GetCurrentlyTradedFuturesContractQuery.Verb,entityId.Format())
            };
            return await e.RequestAsync<FuturesContractV2ReadModel, GetCurrentlyTradedFuturesContractQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetCurrentlyTradedFuturesContracts, async (IActorService e, string symbol) =>
        {
            var entityId = new GetCurrentlyTradedFuturesContractsParameter(symbol); 
            GetCurrentlyTradedFuturesContractsQuery query = new (symbol)
            {
                Subject = new ActorSubject(ActorType.Query, GetCurrentlyTradedFuturesContractsQuery.Actor, GetCurrentlyTradedFuturesContractsQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesContractV2ReadModel[], GetCurrentlyTradedFuturesContractsQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetFuturesContract, async (IActorService e, string contractId) =>
        {
            var entityId = new GetFuturesContractParameter(contractId);
            GetFuturesContractQuery query = new(contractId)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesContractQuery.Actor, GetFuturesContractQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesContractV2ReadModel, GetFuturesContractQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetFuturesContracts, async (IActorService e) =>
        {
            var entityId = new GetFuturesContractsParameter();
            GetFuturesContractsQuery query = new()
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesContractsQuery.Actor, GetFuturesContractsQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesContractV2ReadModel[], GetFuturesContractsQuery>(query);
        });

        // FuturesOptionContractQueryActor queries
        endpoints.MapGet(MarketDataQueryUriPath.GetFuturesOptionContract, async (IActorService e, string contractId) =>
        {
            var entityId = new TomasAI.IFM.Shared.MarketData.QueryParameters.GetFuturesOptionContractParameter(contractId);
            Shared.MarketData.Queries.GetFuturesOptionContractQuery query = new(contractId)
            {
                Subject = new ActorSubject(ActorType.Query, Shared.MarketData.Queries.GetFuturesOptionContractQuery.Actor, Shared.MarketData.Queries.GetFuturesOptionContractQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesOptionContractReadModel, Shared.MarketData.Queries.GetFuturesOptionContractQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetFuturesOptionContracts, async (IActorService e, string symbol) =>
        {
            var entityId = new GetFuturesOptionContractsParameter(symbol);
            GetFuturesOptionContractsQuery query = new(symbol)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractsQuery.Actor, GetFuturesOptionContractsQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesOptionContractReadModel[], GetFuturesOptionContractsQuery>(query);
        });

        endpoints.MapPost(MarketDataQueryUriPath.GetFuturesOptionContractIds, async (IActorService e, string contractIds) =>
        {
            var ids = contractIds?.Split(',') ?? [];
            var query = new GetFuturesOptionContractIdsQuery(ids);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractIdsQuery.Actor, GetFuturesOptionContractIdsQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<string[], GetFuturesOptionContractIdsQuery>(query);
        });

        // YieldCurveRateQueryActor queries
        endpoints.MapGet(MarketDataQueryUriPath.GetLastYieldCurveRate, async (IActorService e) =>
        {
            var entityId = new GetLastYieldCurveRateParameter();
            GetLastYieldCurveRateQuery query = new(true)
            {
                Subject = new ActorSubject(ActorType.Query, GetLastYieldCurveRateQuery.Actor, GetLastYieldCurveRateQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<YieldCurveRateReadModel?, GetLastYieldCurveRateQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetYieldCurveRates, async (IActorService e, DateOnly startDate, DateOnly endDate) =>
        {
            var entityId = new GetYieldCurveRatesParameter(startDate, endDate);
            GetYieldCurveRatesQuery query = new(startDate, endDate)
            {
                Subject = new ActorSubject(ActorType.Query, GetYieldCurveRatesQuery.Actor, GetYieldCurveRatesQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<YieldCurveRateReadModel[], GetYieldCurveRatesQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.YieldCurveRateExists, async (IActorService e, DateOnly valueDate) =>
        {
            var entityId = new GetYieldCurveRateExistsParameter(valueDate);
            GetYieldCurveRateExistsQuery query = new(valueDate)
            {
                Subject = new ActorSubject(ActorType.Query, GetYieldCurveRateExistsQuery.Actor, GetYieldCurveRateExistsQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<ScalarReadModel<bool>, GetYieldCurveRateExistsQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetYieldCurveRateYears, async (IActorService e) =>
        {
            var entityId = new GetYieldCurveRateYearsParameter();
            GetYieldCurveRateYearsQuery query = new(true)
            {
                Subject = new ActorSubject(ActorType.Query, GetYieldCurveRateYearsQuery.Actor, GetYieldCurveRateYearsQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<YieldCurveRateYearsReadModel, GetYieldCurveRateYearsQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetExternalYieldCurveRates, async (IActorService e) =>
        {
            var entityId = new GetExternalYieldCurveRatesParameter();
            GetExternalYieldCurveRatesQuery query = new()
            {
                Subject = new ActorSubject(ActorType.Query, GetExternalYieldCurveRatesQuery.Actor, GetExternalYieldCurveRatesQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<YieldCurveRateReadModel[], GetExternalYieldCurveRatesQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetLastRateOfReturn, async (IActorService e, string symbol, DateOnly valueDate) =>
        {
            var entityId = new GetLastRateOfReturnParameter(symbol, valueDate);
            GetLastRateOfReturnQuery query = new(symbol, valueDate)
            {
                Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<RateOfReturnReadModel, GetLastRateOfReturnQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetTradingDays, async (IActorService e, DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType) =>
        {
            var entityId = new GetTradingDaysParameter(startDate, endDate, marketType, currencyType);
            GetTradingDaysQuery query = new(startDate, endDate, marketType, currencyType)
            {
                Subject = new ActorSubject(ActorType.Query, GetTradingDaysQuery.Actor, GetTradingDaysQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<ScalarReadModel<int>, GetTradingDaysQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetTradingDates, async (IActorService e, DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType) =>
        {
            var entityId = new GetTradingDatesParameter(startDate, endDate, marketType, currencyType);
            GetTradingDatesQuery query = new(startDate, endDate, marketType, currencyType)
            {
                Subject = new ActorSubject(ActorType.Query, GetTradingDatesQuery.Actor, GetTradingDatesQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<DateOnly[], GetTradingDatesQuery>(query);
        });

        endpoints.MapGet(MarketDataQueryUriPath.GetValueDate, async (IActorService e) =>
        {
            var entityId = new GetValueDateParameter();
            GetValueDateQuery query = new()
            {
                Subject = new ActorSubject(ActorType.Query, GetValueDateQuery.Actor, GetValueDateQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<ScalarReadModel<DateOnly>, GetValueDateQuery>(query);
        });


        return endpoints;
    }
}

public static class MarketDataFeedQueries
{
    public static IEndpointRouteBuilder MapMarketDataFeedQueries(this IEndpointRouteBuilder endpoints)
    {

        endpoints.MapPost(MarketDataFeedQueryUriPath.GetFuturesOptionContract, async (IActorService e, [FromQuery] string contractId, [FromBody] Shared.MarketDataFeed.QueryParameters.GetFuturesOptionContractParameter queryParam) =>
        {
            var query = new Shared.MarketDataFeed.Queries.GetFuturesOptionContractQuery(contractId, queryParam.QueryForContract!);
            query.Subject = new ActorSubject(ActorType.Query, Shared.MarketDataFeed.Queries.GetFuturesOptionContractQuery.Actor, Shared.MarketDataFeed.Queries.GetFuturesOptionContractQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesOptionContractReadModel, Shared.MarketDataFeed.Queries.GetFuturesOptionContractQuery>(query);
        });

        endpoints.MapPost(MarketDataFeedQueryUriPath.GetFuturesOptionSpreadData, async (IActorService e, DateOnly valueDate, DateOnly maturityDate, double assetPrice,  double riskFreeRate,  double timeValue, [FromBody] GetFuturesOptionSpreadDataParameter queryParam) =>
        {
            var query = new GetFuturesOptionSpreadDataQuery(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, queryParam.QueryForOptionContracts!);
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesOptionSpreadDataQuery.Actor, GetFuturesOptionSpreadDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesOptionSpreadDataReadModel, GetFuturesOptionSpreadDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetFuturesRiskPositionType, async (IActorService e, DateOnly valueDate, TradeType tradeType) =>
        {
            var query = new GetFuturesRiskPositionTypeQuery(valueDate, tradeType);
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesRiskPositionTypeQuery.Actor, GetFuturesRiskPositionTypeQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<RiskPositionTypeReadModel, GetFuturesRiskPositionTypeQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetIronCondorMarketDataFeed, async (IActorService e, string underlyingContractId, string shortPutOptionContractId, string longPutOptionContractId, string shortCallOptionContractId, string longCallOptionContractId, DateOnly valueDate) =>
        {
            var query = new GetIronCondorMarketDataFeedQuery(underlyingContractId, shortPutOptionContractId, longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetIronCondorMarketDataFeedQuery.Actor, GetIronCondorMarketDataFeedQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<IronCondorMarketDataFeedReadModel, GetIronCondorMarketDataFeedQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetNormalCurveTable, async (IActorService e) =>
        {
            var query = new GetNormalCurveTableQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetNormalCurveTableQuery.Actor, GetNormalCurveTableQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<NormalCurveTableReadModel, GetNormalCurveTableQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetOptionQuoteId, async (IActorService e) =>
        {
            var query = new GetOptionQuoteIdQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetOptionQuoteIdQuery.Actor, GetOptionQuoteIdQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<ScalarValue<int>, GetOptionQuoteIdQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetStreamingRequestId, async (IActorService e) =>
        {
            var query = new GetStreamingRequestIdQuery();
            query.Subject = new ActorSubject(ActorType.Query, GetStreamingRequestIdQuery.Actor, GetStreamingRequestIdQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<ScalarValue<int>, GetStreamingRequestIdQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetFuturesBarData, async (IActorService e, string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate) =>
        {
            var query = new GetFuturesBarDataQuery(contractId, symbol, valueDate, startDate, endDate);
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesBarDataQuery.Actor, GetFuturesBarDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesBarDataReadModel[], GetFuturesBarDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesBarData, async (IActorService e, string contractId, string symbol, DateOnly valueDate) =>
        {
            var query = new GetLastFuturesBarDataQuery(contractId, symbol, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetLastFuturesBarDataQuery.Actor, GetLastFuturesBarDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesBarDataReadModel, GetLastFuturesBarDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodData, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var query = new GetFuturesEodDataQuery(contractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataQuery.Actor, GetFuturesEodDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesEodDataV2ReadModel, GetFuturesEodDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesEodData, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var query = new GetLastFuturesEodDataQuery(contractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetLastFuturesEodDataQuery.Actor, GetLastFuturesEodDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesEodDataV2ReadModel, GetLastFuturesEodDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodDataByDateRange, async (IActorService e, string contractId, DateOnly startDate, DateOnly endDate) =>
        {
            var query = new GetFuturesEodDataByDateRangeQuery(contractId, startDate, endDate);
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataByDateRangeQuery.Actor, GetFuturesEodDataByDateRangeQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesEodDataV2ReadModel[], GetFuturesEodDataByDateRangeQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodDataParameters, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var query = new GetFuturesEodDataParametersQuery(contractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataParametersQuery.Actor, GetFuturesEodDataParametersQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesEodDataParametersReadModel, GetFuturesEodDataParametersQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodMovingAverages, async (IActorService e, string contractId, string symbol, DateOnly valueDate) =>
        {
            var query = new GetFuturesEodDataMovingAveragesQuery(contractId, symbol, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetFuturesEodDataMovingAveragesQuery.Actor, GetFuturesEodDataMovingAveragesQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesEodDataMovingAveragesReadModel, GetFuturesEodDataMovingAveragesQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetVixFuturesEodData, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var query = new GetVixFuturesEodDataQuery(contractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetVixFuturesEodDataQuery.Actor, GetVixFuturesEodDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetLastVixFuturesEodData, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var query = new GetLastVixFuturesEodDataQuery(contractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetLastVixFuturesEodDataQuery.Actor, GetLastVixFuturesEodDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<VixFuturesEodDataReadModel, GetLastVixFuturesEodDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesOptionTickData, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var query = new GetLastFuturesOptionTickDataQuery(contractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetLastFuturesOptionTickDataQuery.Actor, GetLastFuturesOptionTickDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesOptionTickDataV2ReadModel, GetLastFuturesOptionTickDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesTickData, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var query = new GetLastFuturesTickDataQuery(contractId, valueDate);
            query.Subject = new ActorSubject(ActorType.Query, GetLastFuturesTickDataQuery.Actor, GetLastFuturesTickDataQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesTickDataV2ReadModel, GetLastFuturesTickDataQuery>(query);
        });

        endpoints.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesTickDataByTickDate, async (IActorService e, string contractId, DateTime tickDate) =>
        {
            var query = new GetLastFuturesTickDataByTickDateQuery(contractId, tickDate);
            query.Subject = new ActorSubject(ActorType.Query, GetLastFuturesTickDataByTickDateQuery.Actor, GetLastFuturesTickDataByTickDateQuery.Verb, query.EntityId.Format());
            return await e.RequestAsync<FuturesTickDataV2ReadModel, GetLastFuturesTickDataByTickDateQuery>(query);
        });

        return endpoints;
    }
}

public static class OptionPricerQueries
{
    public static IEndpointRouteBuilder MapOptionPricerQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(OptionPricerQueryUriPath.GetSpreadDistribution, async (IActorService e, int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
            => {
                var query = new GetSpreadDistributionQuery(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);
                query = query with { Subject = new ActorSubject(ActorType.Query, GetSpreadDistributionQuery.Actor, GetSpreadDistributionQuery.Verb, query.EntityId.Format()) };
                return await e.RequestAsync<SpreadDistributionReadModel, GetSpreadDistributionQuery>(query);
            });

        return endpoints;
    }
}

public static class MarketDataAnalyticsQueries
{
    public static IEndpointRouteBuilder MapMarketDataAnalyticsQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignal, async (IActorService e, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod) =>
        {
            var entityId = new GetFuturesItiSignalParameter(contractId, valueDate, timePeriod);
            GetFuturesItiSignalQuery query = new (contractId, valueDate, timePeriod)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalQuery.Actor, GetFuturesItiSignalQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesItiSignalV2ReadModel, GetFuturesItiSignalQuery>(query);
        });

        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalData, async (IActorService e, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod) =>
        {
            var entityId = new GetFuturesItiSignalDataParameter(contractId, valueDate, timePeriod);
            GetFuturesItiSignalDataQuery query = new(contractId, valueDate, timePeriod)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalDataQuery.Actor, GetFuturesItiSignalDataQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesItiSignalDataReadModel, GetFuturesItiSignalDataQuery>(query);
        });

        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalMDI, async (IActorService e, string contractId, DateOnly valueDate) =>
        {
            var entityId = new GetFuturesItiSignalMDIParameter(contractId, valueDate);
            GetFuturesItiSignalMDIQuery query = new(contractId, valueDate)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalMDIQuery.Actor, GetFuturesItiSignalMDIQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesItiSignalMDIV2ReadModel[], GetFuturesItiSignalMDIQuery>(query);
        });

        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalMDIByTrend, async (IActorService e, string contractId, DateOnly valueDate, int groupId) =>
        {
            var entityId = new GetFuturesItiSignalMDIByTrendParameter(contractId, valueDate, groupId);
            GetFuturesItiSignalMDIByTrendQuery query = new(contractId, valueDate, groupId)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesItiSignalMDIByTrendQuery.Actor, GetFuturesItiSignalMDIByTrendQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesItiSignalMDIV2ReadModel[], GetFuturesItiSignalMDIByTrendQuery>(query);
        });

        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiTrendDirectionChangedSignals, async (IActorService e, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod) =>
        {
            var entityId = new GetFuturesItiTrendDirectionChangedSignalsParameter(contractId, valueDate, timePeriod);
            GetFuturesItiTrendDirectionChangedSignalsQuery query = new(contractId, valueDate, timePeriod)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesItiTrendDirectionChangedSignalsQuery.Actor, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, entityId.Format()),
                EntityId = entityId,
            };
            return await e.RequestAsync<FuturesItiSignalV2ReadModel[], GetFuturesItiTrendDirectionChangedSignalsQuery>(query);
        });

        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesAtrSignal, async (IActorService e, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength) =>
        {
            var entityId = new FuturesAtrSignalEntityId(contractId!, valueDate, timePeriod, periodLength);
            GetFuturesAtrSignalQuery query = new(contractId, valueDate, timePeriod, periodLength)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesAtrSignalQuery.Actor, GetFuturesAtrSignalQuery.Verb, entityId.Format()),
            };
            return await e.RequestAsync<FuturesAtrSignalReadModel, GetFuturesAtrSignalQuery>(query);
        });

        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesAdxSignal, async (IActorService e, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength) =>
        {
            var entityId = new FuturesAdxSignalEntityId(contractId!, valueDate, timePeriod, periodLength);
            GetFuturesAdxSignalQuery query = new(contractId, valueDate, timePeriod, periodLength)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesAdxSignalQuery.Actor, GetFuturesAdxSignalQuery.Verb, entityId.Format()),
            };
            return await e.RequestAsync<FuturesAdxSignalReadModel, GetFuturesAdxSignalQuery>(query);
        });

        endpoints.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesMacdSignal, async (IActorService e, string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength) =>
        {
            var entityId = new FuturesMacdSignalEntityId(contractId!, valueDate, timePeriod, periodLength);
            GetFuturesMacdSignalQuery query = new(contractId, valueDate, timePeriod, periodLength   )
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesMacdSignalQuery.Actor, GetFuturesMacdSignalQuery.Verb, entityId.Format()),
            };
            return await e.RequestAsync<FuturesMacdSignalReadModel, GetFuturesMacdSignalQuery>(query);
        });

        return endpoints;
    }
}

public static class TradeQueries
{
    public static IEndpointRouteBuilder MapTradeQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(TradeQueryUriPath.GetTradeHistory, async (IActorService e, int orderId) =>
        {
            var query = new GetTradeHistoryQuery(orderId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradeHistoryQuery.Actor, GetTradeHistoryQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<TradeHistoryReadModel[], GetTradeHistoryQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetTradeLimit, async (IActorService e, int tradeId) =>
        {
            var query = new GetTradeLimitQuery(tradeId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradeLimitQuery.Actor, GetTradeLimitQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<TradeLimitReadModel, GetTradeLimitQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetTradeTypeLimit, async (IActorService e, int tradeId, TradeType tradeType) =>
        {
            var query = new GetTradeTypeLimitQuery(tradeId, tradeType);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradeTypeLimitQuery.Actor, GetTradeTypeLimitQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<TradeTypeLimitReadModel, GetTradeTypeLimitQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetTradeQuantity, async (IActorService e, int tradeId) =>
        {
            var query = new GetTradeQuantityQuery(tradeId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradeQuantityQuery.Actor, GetTradeQuantityQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<ScalarReadModel<int>, GetTradeQuantityQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetTradePosition, async (IActorService e, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus) =>
        {
            var query = new GetTradePositionQuery(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradePositionQuery.Actor, GetTradePositionQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<TradePositionReadModel, GetTradePositionQuery>(query);
        });

        return endpoints;
    }
}

public static class OptionTradeQueries
{
    public static IEndpointRouteBuilder MapOptionTradeQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(TradeQueryUriPath.GetOptionLegContractIds, async (IActorService e, int tradeId) =>
        {
            var query = new GetOptionLegContractIdsQuery(tradeId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetOptionLegContractIdsQuery.Actor, GetOptionLegContractIdsQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<string[], GetOptionLegContractIdsQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetOptionTrade, async (IActorService e, int orderId, int tradeId) =>
        {
            var query = new GetOptionTradeQuery(orderId, tradeId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetOptionTradeQuery.Actor, GetOptionTradeQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<OptionTradeReadModel, GetOptionTradeQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetOptionTrades, async (IActorService e, int orderId) =>
        {
            var query = new GetOptionTradesQuery(orderId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetOptionTradesQuery.Actor, GetOptionTradesQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<OptionTradeReadModel[], GetOptionTradesQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetOptionTradeSpreadData, async (IActorService e, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate) =>
        {
            var query = new GetOptionTradeSpreadDataQuery(orderId, tradeId, tradeType, valueDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetOptionTradeSpreadDataQuery.Actor, GetOptionTradeSpreadDataQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<OptionTradeSpreadsDataModel, GetOptionTradeSpreadDataQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetOptionTradeSpreadBarData, async (IActorService e, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateTime startDate, DateTime endDate) =>
        {
            var query = new GetOptionTradeSpreadBarDataQuery(orderId, tradeId, tradeType, valueDate, startDate, endDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetOptionTradeSpreadBarDataQuery.Actor, GetOptionTradeSpreadBarDataQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<OptionTradeSpreadBarsDataModel[], GetOptionTradeSpreadBarDataQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetTradePositions, async (IActorService e, int orderId, int tradeId) =>
        {
            var query = new GetTradePositionsQuery(orderId, tradeId);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradePositionsQuery.Actor, GetTradePositionsQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<TradePositionReadModel[], GetTradePositionsQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetIronCondorTradePrice, async (IActorService e, int tradeId, DateOnly valueDate) =>
        {
            var query = new GetIronCondorTradePriceQuery(tradeId, valueDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetIronCondorTradePriceQuery.Actor, GetIronCondorTradePriceQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<TradePriceReadModel, GetIronCondorTradePriceQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetTradePlanSummary, async (IActorService e, int orderId, int tradeId, DateOnly valueDate) =>
        {
            var query = new GetTradePlanActionQuery(orderId, tradeId, valueDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradePlanActionQuery.Actor, GetTradePlanActionQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<TradePlanActionReadModel[], GetTradePlanActionQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetTradePositionTradeTypes, async (IActorService e, int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus) =>
        {
            var query = new GetTradePositionTradeTypesQuery(orderId, tradeId, valueDate, daysToExpiry, tradeStatus);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetTradePositionTradeTypesQuery.Actor, GetTradePositionTradeTypesQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<string[], GetTradePositionTradeTypesQuery>(query);
        });

        endpoints.MapGet(TradeQueryUriPath.GetIronCondorMDILimit, async (IActorService e, int orderId, int tradeId, DateOnly valueDate) =>
        {
            var query = new GetIronCondorMDILimitQuery(orderId, tradeId, valueDate);
            query = query with { Subject = new ActorSubject(ActorType.Query, GetIronCondorMDILimitQuery.Actor, GetIronCondorMDILimitQuery.Verb, query.EntityId.Format()) };
            return await e.RequestAsync<IronCondorMDILimitDataModel, GetIronCondorMDILimitQuery>(query);
        });

        return endpoints;
    }
}

public static class SystemAdminQueries
{
    public static IEndpointRouteBuilder MapSystemAdminQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(SystemAdminQueryUriPath.GetDatabaseNames, async (IActorService e) =>
        {
            var entityId = new GetDatabaseNamesParameter();
            var query = new GetDatabaseNamesQuery()
            {
                Subject = new ActorSubject(ActorType.Query, GetDatabaseNamesQuery.Actor, GetDatabaseNamesQuery.Verb, entityId.Format())
            };
            return await e.RequestAsync<DatabaseNamesReadModel, GetDatabaseNamesQuery>(query);
        });

        return endpoints;
    }
}



