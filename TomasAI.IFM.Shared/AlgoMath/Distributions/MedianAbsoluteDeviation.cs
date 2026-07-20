using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;

namespace TomasAI.IFM.Shared.AlgoMath.Distributions
{
    public  class MedianAbsoluteDeviation
    {
        IEnumerable<double> _samples;
        double _median;
        double _medianAbsDev;

        
        private MedianAbsoluteDeviation(IEnumerable<double> samples)
        {
            _samples = samples;
            
            // get median...
            _median = _samples.OrderBy(e => e).ToArray()[(int)(_samples.Count() / 2)];

            // get the absolute deviations from the median...
            var absDevsFromMedian = _samples.Select(x => Math.Abs(x - _median)).ToList();

            // get the median of the absolute deviations...
            _medianAbsDev = absDevsFromMedian.OrderBy(e => e).ToArray()[(int)(absDevsFromMedian.Count / 2)];

            this.ScalingFactor = 3.0;
        }

        public static MedianAbsoluteDeviation Estimate(IEnumerable<double> samples) => new MedianAbsoluteDeviation(samples);

        public double Median => _median;
        public double MedianAbsDev => _medianAbsDev;

        public double ScalingFactor { get; set; }

        public double Score(double value) => Math.Sqrt(value) / (_median + (this.ScalingFactor * _medianAbsDev));

        public bool IsOutlier(double value) => (value > (_median + (this.ScalingFactor * _medianAbsDev))) || (value < (_median - (this.ScalingFactor * _medianAbsDev)));


    }

}
