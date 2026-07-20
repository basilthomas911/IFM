using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketData
{
    public class EquityMarket
    {
        public string Symbol { get; set; }
        public string Exchange { get; set; }
        public TimeSpan MarketOpen { get; set; }
        public TimeSpan MarketClose { get; set; }
    }
}
