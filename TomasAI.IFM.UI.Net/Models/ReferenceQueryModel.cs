using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.UI.Net.Models;

public class ReferenceQueryModel : BaseModel<ReferenceQueryModel>
{
    readonly IReferenceQueryApi _queryApi;

    /// <summary>
    /// create reference model
    /// </summary>
    /// <param name="queryApi"></param>
    public ReferenceQueryModel(IReferenceQueryApi queryApi)
    {
        _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
    }

    /// <summary>
    /// load trade history for selected trade order
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadDefaultFuturesContractDefinitionsAsync(Action<DefaultFuturesContractDefinitionsReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetDefaultFuturesContractDefinitionsAsync(), onCompleted);

    /// <summary>
    /// load futures option strike price definitions
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadFuturesOptionStrikePriceDefinitionsAsync(Action<FuturesOptionStrikePriceReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesOptionStrikePriceDefinitionsAsync(), onCompleted);

    /// <summary>
    /// return new fund id
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task NewFundIdAsync(Action<int> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetNextSeedIdAsync("FundId"), vm => onCompleted(vm.Value));

    /// <summary>
    /// return new order id
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task NewOrderIdAsync(Action<int> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetNextSeedIdAsync("OrderId"), vm => onCompleted(vm.Value));

    /// <summary>
    /// return current order id
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetCurrentOrderIdAsync(Action<int> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetCurrentSeedIdAsync("OrderId"), vm => onCompleted(vm.Value));

    /// <summary>
    /// return new trade id
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task NewTradeIdAsync(Action<int> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetNextSeedIdAsync("TradeId"), vm => onCompleted(vm.Value));

    /// <summary>
    /// return current trade id
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetCurrentTradeIdAsync(Action<int> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetCurrentSeedIdAsync("TradeId"), vm => onCompleted(vm.Value));

    /// <summary>
    /// load marketdata definition types
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadMarketDataDefinitionTypesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetMarketDataDefinitionTypesAsync(), onCompleted);

    /// <summary>
    /// load reference data definition types
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadReferenceDataDefinitionTypesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(_queryApi.GetReferenceDataDefinitionTypesAsync, onCompleted);

    /// <summary>
    /// load system admin function types
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadSystemAdminFunctionTypesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(_queryApi.GetSystemAdminFunctionTypesAsync, onCompleted);

    /// <summary>
    /// load security symbols
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadSymbolsAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypesAsync("Symbol"), onCompleted);

    /// <summary>
    /// load security types
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadSecurityTypesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypesAsync("SecurityType"), onCompleted);

    /// <summary>
    /// load currencies
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadCurrenciesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypesAsync("Currency"), onCompleted);

    /// <summary>
    /// load exchanges
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadExchangesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypesAsync("Exchange"), onCompleted);

    /// <summary>
    /// load multipliers
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadMultipliersAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypesAsync("Multiplier"), onCompleted);

    /// <summary>
    /// load option types
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task LoadOptionTypesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypesAsync("OptionType"), onCompleted);

    /// <summary>
    /// load economic calendar
    /// </summary>
    /// <param name="todaysDate"></param>
    /// <param name="calendarViewType"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadEconomicCalendarAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType, string countryCode,  Action<EconomicCalendarReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetEconomicCalendarsAsync(todaysDate, calendarViewType, countryCode), onCompleted);

    /// <summary>
    /// load economic calendar
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadEconomicCalendarsAsync(DateOnly eventDate, string countryCode, Action<EconomicCalendarReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetEconomicCalendarsAsync(eventDate.ToDateTime(TimeOnly.MinValue), EconomicCalendarViewType.Today, countryCode), onCompleted);

    /// <summary>
    /// load economic calendar country codes
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadEconomicCalendarCountryCodesAsync(Action<EconomicCalendarCountryCodeReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetEconomicCalendarCountryCodesAsync(), onCompleted);


    /// <summary>
    /// load economic calendar date
    /// </summary>
    /// <param name="todaysDate"></param>
    /// <param name="calendarViewType"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarViewType, Action<string> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetEconomicCalendarDateAsync(todaysDate, calendarViewType), onCompleted);

    /// <summary>
    /// load lookup type names
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadLookupTypeNamesAsync( Action<string[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypeNamesAsync(), onCompleted);

    /// <summary>
    /// load lookup type short codes by lookup type name
    /// </summary>
    /// <param name="lookupTypeName"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadLookupTypeShortCodesAsync(string lookupTypeName, Action<LookupTypeShortCodeReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetLookupTypeShortCodesAsync(lookupTypeName), onCompleted);

    /// <summary>
    /// load lookup types
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadLookupTypesAsync(Action<ICollection<LookupTypeReadModel>> onCompleted)
        => await ExecuteAsync(_queryApi.GetLookupTypesAsync, onCompleted);

    /// <summary>
    /// get external economic calendars
    /// </summary>
    /// <returns></returns>
    public async Task GetExternalEconomicCalendarsAsync(Action<EconomicCalendarReadModel[]> onCompleted)
        => await ExecuteAsync(_queryApi.GetExternalEconomicCalendarsAsync, onCompleted);

    /// <summary>
    /// load MDI forward loss ratios
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadMDIFowardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType, Action<MDIForwardLossRatioReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetMDIForwardLossRatiosAsync(trendDirection, tradeType), onCompleted);

}
