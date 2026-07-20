using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public class TickBidAskData
    {
        private readonly DateTime _tickDate;
        private readonly long _tickTime;
        private readonly double _bidPrice;
        private readonly double _askPrice;
        private readonly int _bidSize;
        private readonly int _askSize;

        public DateTime TickDate => _tickDate;
        public long TickTime => _tickTime;
        public double BidPrice => _bidPrice;
        public double AskPrice => _askPrice;
        public int BidSize => _bidSize;
        public int AskSize => _askSize;

        public TickBidAskData(
            DateTime tickDate,
            long tickTime,
            double bidPrice,
            double askPrice,
            int bidSize,
            int askSize)
        {
            _tickDate = tickDate;
            _tickTime = tickTime;
            _bidPrice = bidPrice;
            _askPrice = askPrice;
            _bidSize = bidSize;
            _askSize = askSize;
        }
    }
}
