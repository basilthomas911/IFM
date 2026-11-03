using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class TradeActionExtensions
    {

        extension(TradeAction value)
        {
            public string ToStringFast() => value switch
            {
                TradeAction.Buy => nameof(TradeAction.Buy),
                TradeAction.Sell => nameof(TradeAction.Sell),
                _ => value.ToString()
            };
        }
    }
}
