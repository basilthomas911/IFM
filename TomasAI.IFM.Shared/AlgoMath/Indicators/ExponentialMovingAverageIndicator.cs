using System;
using System.Collections.Generic;
using System.Linq;

namespace TomasAI.IFM.Shared.AlgoMath.Indicators
{
    public class ExponentialMovingAverageIndicator
    {
        readonly double _weightingMultiplier;
        readonly List<double> _values;
        bool _isInitialized;
        double _previousAverage;
    
        /// <summary>
        /// exponential moving average indicator constructor
        /// </summary>
        /// <param name="lookback"></param>
        public ExponentialMovingAverageIndicator(int lookback)
        {
            _weightingMultiplier = 2.0 / (lookback+1) ;
            _values = new();
        }

        public double Value { get; private set; }
        public double Slope { get; private set; }
        public double SMA => _values.Average();

        /// <summary>
        /// estimate average and slope at this data point
        /// </summary>
        /// <param name="dataPoint"></param>
        public void Estimate(double dataPoint)
        {
            _values.Add(dataPoint);
            if (!_isInitialized)
            {
                Value = dataPoint;
                Slope = 0;
                _previousAverage = Value;
                _isInitialized = true;
                return;
            }

            Value = ((dataPoint - _previousAverage) * _weightingMultiplier) + _previousAverage;
            Slope = Value - _previousAverage;

            //update previous average
            _previousAverage = Value;
        }
    }
 
    
}
