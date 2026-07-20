using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Events;

namespace TomasAI.IFM.Models
{
    public class TradePlacementCommandModel : BaseModel<TradePlacementCommandModel>
    {
        readonly ITradePlacementCommandApi _commandApi;

        /// <summary>
        /// create trade command model
        /// </summary>
        /// <param name="commandApi"></param>
        public TradePlacementCommandModel(
            ITradePlacementCommandApi commandApi)
        {
            _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
        }

        /// <summary>
        /// start trade placement signal service
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        public async Task StartTradePlacementAsync(string contractId, DateTime valueDate)
            => await ExecuteCommandAsync(() => _commandApi.StartTradePlacementAsync(TradePlacementId.Create(contractId, valueDate)));

        /// <summary>
        /// stop trade placement signal service
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        public async Task StopTradePlacementAsync(string contractId, DateTime valueDate)
            => await ExecuteCommandAsync(() => _commandApi.StopTradePlacementAsync(TradePlacementId.Create(contractId, valueDate)));

    }
}
