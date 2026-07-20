using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.ViewModels.Trade;

namespace TomasAI.IFM.Views.Trade
{
    public interface ITradeOrderControl
    {
        DateTime MaturityDate { get; }
        void RemoveTrade(int fundid, int orderId, int tradeId);
        void SubmitOrder(DateTime tradeDate, OrderActionType orderAction, TradeOrderConfirmationViewModel tradeOrderConfirmation, Action<Guid> setCommandId);
        void LiveFeed(bool enabled);
        void SetNearestStrikePrices();
        void OrderActionTypeChanged(OrderActionType orderActionType);
    }
}
