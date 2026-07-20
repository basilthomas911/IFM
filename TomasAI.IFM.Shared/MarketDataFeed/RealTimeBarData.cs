using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public class RealTimeBarData
    {
        private readonly long _time;
        private readonly double _open;
        private readonly double _high;
        private readonly double _low;
        private readonly double _close;
        private readonly long _volume;
        private readonly double _vwap;
        private readonly int _count;

        public long Time => _time;
        public double Open => _open;
        public double High => _high;
        public double Low => _low;
        public double Close => _close;
        public long Volume => _volume;
        public double VWap => _vwap;
        public int Count => _count;

        public RealTimeBarData(
            long time,
            double open,
            double high,
            double low,
            double close,
            long volume,
            double vwap,
            int count)
        {
            _time = time;
            _open = open;
            _high = high;
            _low = low;
            _close = close;
            _volume = volume;
            _vwap = vwap;
            _count = count;
        }

    }
}
