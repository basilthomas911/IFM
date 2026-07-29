using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Domain.Trade.Shared.AlgoTrader
{
    public interface ITradeAlgorithm
    {
        TradePlanUpdatedEvent ExecuteAlgorithm();
    }
}
