using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    public enum ActionType
    {
        PlaceOpenOrder,
        PlaceCloseOrder,
        PlaceCancelOrder,
        PlaceUpdateOrder,
        FillOpenOrder,
        FillCloseOrder,
        OpenTradePosition,
        CloseTradePosition,
        HoldTradePosition,
        OpenHedgePosition,
        HedgeTradePosition,
        CloseHedgePosition,
        ExitTradePosition,
        ExitHedgePosition,
        WarnTradePosition
    }
}
