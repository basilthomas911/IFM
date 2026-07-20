using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.Client
{
    public class ReferenceQueryApi : IReferenceQueryApi
    {
        readonly IQueryService _querySvc;
        readonly string _controller;

        public ReferenceQueryApi(IQueryService querySvc)
        {
            _querySvc = IsArgumentNull.Set(querySvc);
            _controller = "Reference";
        }

        /// <summary>
        /// return defualt futures contract definitions
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetDefaultFuturesContractDefinitionsQuery { }, _controller);

        /// <summary>
        /// return lookup types by lookup type name
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetLookupTypesAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypesQuery{}, _controller);

        /// <summary>
        /// return lookup types by lookup type name
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetLookupTypesAsync(string lookupTypeName)
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypeQuery { LookupTypeName = lookupTypeName }, _controller);

        /// <summary>
        /// return lookup type names
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<string[]>> GetLookupTypeNamesAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypeNamesQuery { }, _controller);

        /// <summary>
        /// return lookup type short codes by lookup type name
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName)
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypeShortCodesQuery { LookupTypeName = lookupTypeName }, _controller);

        /// <summary>
        /// return list of market type definitions
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetMarketDataDefinitionTypesAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypeQuery { LookupTypeName = "MarketDataDefinitionType" }, _controller);

        /// <summary>
        /// return list of market type definitions
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetReferenceDataDefinitionTypesAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypeQuery { LookupTypeName = "ReferenceDataDefinitionType" }, _controller);

        /// <summary>
        /// return list of system admin function types
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<LookupTypeReadModel[]>> GetSystemAdminFunctionTypesAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypeQuery { LookupTypeName = "SystemAdminFunctionType" }, _controller);

        public async Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType)
            => await _querySvc.ExecuteApiQueryAsync(new GetNextSeedIdQuery { SeedType = seedType }, _controller);

        public async Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType)
            => await _querySvc.ExecuteApiQueryAsync(new GetCurrentSeedIdQuery { SeedType = seedType }, _controller);

        public async Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesOptionStrikePriceDefinitionsQuery { }, _controller);

        /// <summary>
        /// check if short code exists for selceted lookup type
        /// </summary>
        /// <param name="lookupTypeName"></param>
        /// <param name="shortCode"></param>
        /// <returns></returns>
        public async Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode)
            => await _querySvc.ExecuteApiQueryAsync(new GetLookupTypeShortCodeExistsQuery { LookupTypeName = lookupTypeName, ShortCode = shortCode },_controller);

        /// <summary>
        /// return economic calendar by calendar view type
        /// </summary>
        /// <param name="todaysDate"></param>
        /// <param name="calendarViewType"></param>
        /// <param name="countryCode"></param>
        /// <returns></returns>
        public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode)
            => await _querySvc.ExecuteApiQueryAsync(new GetEconomicCalendarQuery { TodaysDate = todaysDate, CalendarViewType = calendarViewType, CountryCode = countryCode }, _controller);

        /// <summary>
        /// return all economic calendars
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetEconomicCalendarAllQuery{ },_controller);

        /// <summary>
        /// return economic calendars from external source
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetExternalEconomicCalendarsQuery { }, _controller);

        /// <summary>
        /// return economic calendar date
        /// </summary>
        /// <param name="todaysDate"></param>
        /// <param name="calendarViewType"></param>
        /// <returns></returns>
        public async Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType)
            => await _querySvc.ExecuteApiQueryAsync(new GetEconomicCalendarDateQuery { TodaysDate = todaysDate, CalendarViewType = calendarViewType }, _controller);

        /// <summary>
        /// return economic calendar country codes
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetEconomicCalendarCountryCodesQuery { }, _controller);

        /// <summary>
        /// return MDI forward loss ratios
        /// </summary>
        /// <param name="trendDirection"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
            => await _querySvc.ExecuteApiQueryAsync(new GetMDIForwardLossRatiosQuery { TrendDirection = trendDirection, TradeType = tradeType}, _controller);
    }
}
