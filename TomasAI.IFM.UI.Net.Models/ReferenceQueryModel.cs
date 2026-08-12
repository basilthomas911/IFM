using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;

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

    public Task LoadFuturesOptionStrikePriceDefinitionsAsync(Func<FuturesOptionStrikePriceReadModel, Task> onCompleted)
        => ExecuteAsync(() => _queryApi.GetFuturesOptionStrikePriceDefinitionsAsync(), onCompleted);

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
    /// return new trade id
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task NewTradeIdAsync(Action<int> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetNextSeedIdAsync("TradeId"), vm => onCompleted(vm.Value));

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

    public Task LoadLookupTypesAsync(Func<ICollection<LookupTypeReadModel>, Task> onCompleted)
        => ExecuteAsync(_queryApi.GetLookupTypesAsync, onCompleted);

    /// <summary>
    /// load MDI forward loss ratios
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task LoadMDIFowardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType, Action<MDIForwardLossRatioReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetMDIForwardLossRatiosAsync(trendDirection, tradeType), onCompleted);

    public Task LoadMDIFowardLossRatiosAsync(
        IntrinsicTimeTrendType trendDirection,
        TradeType tradeType,
        Func<MDIForwardLossRatioReadModel[], Task> onCompleted)
        => ExecuteAsync(() => _queryApi.GetMDIForwardLossRatiosAsync(trendDirection, tradeType), onCompleted);

}
