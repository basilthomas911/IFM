using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.ViewModels.Reference
{
    public class ReferenceViewModel
    {
        readonly IAppRoot _appRoot;
        List<LookupTypeReadModel> _mktDataDefTypes;
        List<MDIForwardLossRatioReadModel> _mdiForwardLossRatios;

        public ReferenceViewModel(IAppRoot appRoot)
        {
            _appRoot = appRoot;
        }

        public void LoadReferenceDataDefinitionTypes(Action<LookupTypeReadModel[]> onDataLoad)
            => _appRoot.GetModel<ReferenceQueryModel>().Execute(async ctlr =>
                await ctlr.LoadReferenceDataDefinitionTypesAsync(mktDataDefTypes => {
                    _mktDataDefTypes = new List<LookupTypeReadModel>();
                    if (mktDataDefTypes != null && mktDataDefTypes.Length > 0)
                        _mktDataDefTypes.AddRange(mktDataDefTypes);
                    onDataLoad(mktDataDefTypes);
                }));

        public LookupTypeReadModel GetMarketDefinitionType(int index)
            => _mktDataDefTypes != null && _mktDataDefTypes.Count > 0 ? _mktDataDefTypes[index] : null;

        public void LoadMDIForwardLossRatios(IntrinsicTimeTrendType trendDirection, TradeType tradeType, Action<MDIForwardLossRatioReadModel[]> onDataLoad)
            => _appRoot.GetModel<ReferenceQueryModel>().Execute(async model =>
                await model.LoadMDIFowardLossRatiosAsync(trendDirection, tradeType, mdiForwardLossRatios => {
                    _mdiForwardLossRatios = new List<MDIForwardLossRatioReadModel>();
                    if (mdiForwardLossRatios != null && mdiForwardLossRatios.Length > 0)
                        _mdiForwardLossRatios.AddRange(mdiForwardLossRatios);
                    onDataLoad(mdiForwardLossRatios);
                }));
    }
}
