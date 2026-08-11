using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData
{
    public class PlaceTradeUIViewModel
    {
        public string PlaceTrade { get; private set; }
        public PresentationColorRole PlaceTradeForeColor { get; private set; }
        public PresentationColorRole PlaceTradeBackColor { get; private set; }

        public PlaceTradeUIViewModel(IEvent @event)
        {
            PlaceTrade = GetTradePlacementText();
            PlaceTradeForeColor = PresentationColorRole.DarkText;
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

            PresentationColorRole GetTradePlacementBackColor()
               => @event switch
               {
                   TradePlacementSetEvent => PresentationColorRole.Positive,
                   TradePlacementWaitEvent => PresentationColorRole.Caution,
                   TradePlacementClearedEvent => PresentationColorRole.Negative,
                   _ => PresentationColorRole.DarkSurface
               };
        }
    }
}
