using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade
{
    public class TradeOrderConfirmationViewModel
    {
        private Func<TradeOrderConfirmationViewModel, bool> _showOrderConfirmationAction;
        public TradeOrderReadModel TradeOrder { get; set; } = null!;
         public TradeOrderConfirmationViewModel(Func<TradeOrderConfirmationViewModel, bool> showOrderConfirmationAction)
            => _showOrderConfirmationAction = showOrderConfirmationAction;

        public bool ShowOrderConfirmation() => _showOrderConfirmationAction(this);
    }
}
