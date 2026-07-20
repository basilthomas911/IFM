using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public class FuturesTickDataPrimaryKey
    {
   
        public static FuturesTickDataPrimaryKey Create(string symbol, string lastTradeDate)
            => new FuturesTickDataPrimaryKey(symbol, lastTradeDate);

        private FuturesTickDataPrimaryKey(string symbol, string lastTradeDate)
        {
            Symbol = symbol;
            LastTradeDate = lastTradeDate;
        }

        public string Symbol { get; }
        public string LastTradeDate { get; }

        public override string ToString()
            => JsonConvert.SerializeObject(new FuturesTickDataPrimaryKey(Symbol, LastTradeDate), Formatting.None);
    }
}
