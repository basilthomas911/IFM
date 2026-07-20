using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
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

    public static class ActionTypeExtensions
    {
        public static string ToStringFast(this ActionType value) => value switch
        {
            ActionType.PlaceOpenOrder => nameof(ActionType.PlaceOpenOrder),
            ActionType.PlaceCloseOrder => nameof(ActionType.PlaceCloseOrder),
            ActionType.PlaceCancelOrder => nameof(ActionType.PlaceCancelOrder),
            ActionType.PlaceUpdateOrder => nameof(ActionType.PlaceUpdateOrder),
            ActionType.FillOpenOrder => nameof(ActionType.FillOpenOrder),
            ActionType.FillCloseOrder => nameof(ActionType.FillCloseOrder),
            ActionType.OpenTradePosition => nameof(ActionType.OpenTradePosition),
            ActionType.CloseTradePosition => nameof(ActionType.CloseTradePosition),
            ActionType.HoldTradePosition => nameof(ActionType.HoldTradePosition),
            ActionType.OpenHedgePosition => nameof(ActionType.OpenHedgePosition),
            ActionType.HedgeTradePosition => nameof(ActionType.HedgeTradePosition),
            ActionType.CloseHedgePosition => nameof(ActionType.CloseHedgePosition),
            ActionType.ExitTradePosition => nameof(ActionType.ExitTradePosition),
            ActionType.ExitHedgePosition => nameof(ActionType.ExitHedgePosition),
            ActionType.WarnTradePosition => nameof(ActionType.WarnTradePosition),
            _ => value.ToString()
        };
    }
}
