using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    public static class RequestID
    {
        public static readonly int Futures = 1001;
        public static readonly int ShortOption = 1002;
        public static readonly int ShortOptionGreeks = 1003;
        public static readonly int LongOption = 1004;
        public static readonly int LongOptionGreeks = 1005;
        public static readonly int FuturesRTBars = 1006;
        public static readonly int FuturesContract = 1007;
        public static readonly int FuturesOptionContract = 1008;
    }
}
