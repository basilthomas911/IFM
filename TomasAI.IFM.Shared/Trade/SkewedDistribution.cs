using System;
using System.Collections.Generic;
using System.Linq;

namespace TomasAI.IFM.Shared.Trade
{
    public class SkewedDistribution
    {
        private IEnumerable<double> _samples;
        private Dictionary<string, int> _frequencyMap;
        private int _count;
        private double _variance;
        private double _median;

        public static SkewedDistribution Estimate(IEnumerable<double> samples) => new SkewedDistribution(samples);

        public int Count => _count;

        public double Mean => _samples.Average();

        public double Variance => _variance;

        public double StdDev => (Math.Sqrt(_variance) / 2.0);

        public double Median => _median;
        public double Quintile => _median * 0.4;

        private SkewedDistribution(IEnumerable<double> samples)
        {
            _count = 0;
            _frequencyMap = new Dictionary<string, int>();
            _samples = samples.Select(sample => ((int)(sample / 0.01) * 0.01));
            for (var index = 0.0; index < 1.0; index += 0.01)
            {
                if (!_frequencyMap.ContainsKey($"{index:F2}"))
                    _frequencyMap.Add($"{index:F2}", 0);
                var count = _frequencyMap[$"{index:F2}"];
                count += _samples.Where(sample => sample >= index && sample < (index + 0.01)).Count();
                _frequencyMap[$"{index:F2}"] = count;
                _count += count;
            }
            _variance = CalculateVariance();
            _median = GetMedian();
        }

        public double CumulativeDistribution(double x)
        {
            var probability = 0.0;
            for (var index = 0.0; index < 1.0; index += 0.01)
            {
                if (index > x) break;
                probability += _frequencyMap.ContainsKey($"{index:F2}")
                    ? (double)_frequencyMap[$"{index:F2}"] / (double)_count
                    : 0.0;
            }
            return probability;
        }

        /// <summary>
        /// calculate variance
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// 1/N * ∑(x−μ)2
        /// </remarks>
        private double CalculateVariance()
        {
            var mean = Mean;
            return _samples.Select(sample => Math.Pow(sample - mean, 2)).Sum() * (1.0 / (double)_count);
        }

        private double GetMedian()
        {
            var orderedSamples = _samples.OrderBy(e => e).ToArray();
            return orderedSamples[orderedSamples.Count() / 2];
        }
    }
}
