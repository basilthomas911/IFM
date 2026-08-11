using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

public class ReferenceViewModel(IAppRoot appRoot)
{
    readonly IAppRoot _appRoot = appRoot;
    List<LookupTypeReadModel> _mktDataDefTypes = [];
    List<MDIForwardLossRatioReadModel> _mdiForwardLossRatios = [];
    public Action OnDisableAllButtons = () => { };
    public Action<bool> OnEnableMarketSelector = (enabled) => { };

    public Task LoadReferenceDataDefinitionTypes(Action<LookupTypeReadModel[]> onDataLoad)
        => _appRoot.GetModel<ReferenceQueryModel>().ExecuteAsync(async ctlr =>
            await ctlr.LoadReferenceDataDefinitionTypesAsync(mktDataDefTypes => {
                _mktDataDefTypes = [];
                if (mktDataDefTypes is not null && mktDataDefTypes.Count > 0)
                    _mktDataDefTypes.AddRange(mktDataDefTypes);
                onDataLoad([.. mktDataDefTypes!]);
            }));

    public LookupTypeReadModel GetMarketDefinitionType(int index)
        => _mktDataDefTypes != null && _mktDataDefTypes.Count > 0 ? _mktDataDefTypes[index] : null!;

    public Task LoadMDIForwardLossRatios(IntrinsicTimeTrendType trendDirection, TradeType tradeType, Action<MDIForwardLossRatioReadModel[]> onDataLoad)
        => _appRoot.GetModel<ReferenceQueryModel>().ExecuteAsync(async model =>
            await model.LoadMDIFowardLossRatiosAsync(trendDirection, tradeType, mdiForwardLossRatios => {
                _mdiForwardLossRatios = new List<MDIForwardLossRatioReadModel>();
                if (mdiForwardLossRatios != null && mdiForwardLossRatios.Length > 0)
                    _mdiForwardLossRatios.AddRange(mdiForwardLossRatios);
                onDataLoad(mdiForwardLossRatios!);
            }));
}
