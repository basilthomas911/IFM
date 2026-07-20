using System.Drawing;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.ViewModels.MarketData
{
    public class PlaceTradeUIViewModel
    {
        public string PlaceTrade { get; private set; }
        public Color PlaceTradeForeColor { get; private set; }
        public Color PlaceTradeBackColor { get; private set; }

        public PlaceTradeUIViewModel(IEvent @event)
        {
            PlaceTrade = GetTradePlacementText();
            PlaceTradeForeColor = Color.Black;
            PlaceTradeBackColor = GetTradePlacementBackColor();
            return;

            string GetTradePlacementText()
                => @event switch
                {
                    TradePlacementSetEvent => "Yes",
                    TradePlacementWaitEvent => "Wait...",
                    TradePlacementClearedEvent => "No",
                    _ => string.Empty
                };

            Color GetTradePlacementBackColor()
               => @event switch
               {
                   TradePlacementSetEvent => Color.LimeGreen,
                   TradePlacementWaitEvent => Color.Yellow,
                   TradePlacementClearedEvent => Color.Red,
                   _ => Color.Black
               };
        }
    }
}
