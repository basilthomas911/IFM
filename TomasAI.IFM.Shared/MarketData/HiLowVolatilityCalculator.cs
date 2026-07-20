using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Shared.MarketData
{
    public class HiLowVolatilityCalculator
    {
        private int _windowSize;
        private double[] _hiLowData;
        private List<StdDevData> _stdDevList;

        public double ExpMovAvg => GetExpMovAvg();
        public double HiLowVolatility => _hiLowData[0];

        /// <summary>
        /// create std dev calculator
        /// </summary>
        /// <param name="windowSize"></param>
        /// <param name="eodBarData"></param>
        public HiLowVolatilityCalculator(int windowSize, IEodBarData[] eodBarData)
        {
            _windowSize = windowSize;
            _hiLowData = eodBarData
                .Select(e => (e.HighPrice - e.LowPrice) / (0.01 * e.ClosePrice))
                .ToArray();
        }

        private double GetExpMovAvg()
        {
            var ema = _hiLowData.Skip(_windowSize).Take(_windowSize).Average(e => e);
            _stdDevList = new List<StdDevData>();
            for (var i = 0; i < _windowSize; i++)
            {
                var hiLowVol = _hiLowData[_windowSize - 1 - i];
                var stdDevData = new StdDevData { Ema = hiLowVol * 2.0 / (_windowSize + 1.0) + ema * (1.0 - 2.0 / (_windowSize + 1.0)) };
                _stdDevList.Add(stdDevData);
                ema = stdDevData.Ema;
            }
            _stdDevList.Reverse();
            return _stdDevList[0].Ema;
        }

        private class StdDevData
        {
            public double Ema { get; set; }
            public double CloseMean { get; set; }
            public double CloseMeanSquared { get; set; }
        }
    }
}
