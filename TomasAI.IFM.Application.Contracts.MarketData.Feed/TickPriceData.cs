using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    public record TickPriceData(
            DateTime TickDate,
            long TickTime,
            double Price,
            int Size,
            string Exchange)
    {
    }
}
