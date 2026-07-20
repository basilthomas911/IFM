using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.ViewModels.MarketData
{
    public class MarketDataViewModel
    {
        private readonly IAppRoot _appRoot;
        private readonly Guid _siteId;
        private List<LookupTypeReadModel> _mktDataDefTypes;

        public MarketDataViewModel(IAppRoot appRoot)
        {
            _appRoot = appRoot;
            _siteId = Guid.NewGuid();
        }

        public void LoadMarketDefinitionTypes(Action<LookupTypeReadModel[]> onDataLoad)
            => _appRoot.GetModel<ReferenceQueryModel>().Execute(async model =>
                await model.LoadMarketDataDefinitionTypesAsync(mktDataDefTypes => {
                    _mktDataDefTypes = new List<LookupTypeReadModel>();
                    if (mktDataDefTypes?.Length > 0)
                        _mktDataDefTypes.AddRange(mktDataDefTypes);
                    onDataLoad?.Invoke(mktDataDefTypes);
                }));

        public LookupTypeReadModel GetMarketDefinitionType(int index)
            => _mktDataDefTypes?.Count > 0 ? _mktDataDefTypes[index] : LookupTypeReadModel.Default;

    }
}
