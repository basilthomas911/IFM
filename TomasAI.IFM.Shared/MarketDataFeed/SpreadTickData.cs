using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public class SpreadTickData
    {
        private readonly double _assetPrice;
        private readonly double _shortBid;
        private readonly double _shortAsk;
        private readonly double _shortVol;
        private readonly double _longBid;
        private readonly double _longAsk;
        private readonly double _longVol;
        private readonly double _delta;
        private readonly double _gamma;
        private readonly double _theta;
        private readonly double _vega;

        public double AssetPrice => _assetPrice;
        public double ShortBid => _shortBid;
        public double ShortAsk => _shortAsk;
        public double ShortVolatility => _shortVol;
        public double LongBid => _longBid;
        public double LongAsk => _longAsk;
        public double LongVolatility => _longVol;
        public double Delta => _delta;
        public double Gamma => _gamma;
        public double Theta => _theta;
        public double Vega => _vega;

        public SpreadTickData(
            double assetPrice,
            double shortBid,
            double shortAsk,
            double shortVol,
            double longBid,
            double longAsk,
            double longVol,
            double delta,
            double gamma,
            double theta,
            double vega)
        {
            _assetPrice = assetPrice;
            _shortBid = shortBid;
            _shortAsk = shortAsk;
            _shortVol = shortVol;
            _longBid = longBid;
            _longAsk = longAsk;
            _longVol = longVol;
            _delta = delta;
            _gamma = gamma;
            _theta = theta;
            _vega = vega;
        }

    }
}
