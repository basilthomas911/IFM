using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Trade
{
    public enum TradeAction
    {
        Buy,
        Sell
    }

    public static class TradeActionExtensions
    {
        public static string ToStringFast(this TradeAction value) => value switch
        {
            TradeAction.Buy => nameof(TradeAction.Buy),
            TradeAction.Sell => nameof(TradeAction.Sell),
            _ => value.ToString()
        };
    }
}
