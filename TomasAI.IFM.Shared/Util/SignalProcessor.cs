using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using MathNet.Numerics.Distributions;
using TomasAI.IFM.Shared.AlgoMath.Distributions;

namespace TomasAI.IFM.Shared.Util
{
    public class SignalProcessor<TData>
    {
        private static object _lock = new object();
        private Stack<TData> _signalMap;

        public SignalProcessor()
        {
            _signalMap = new Stack<TData>();
        }

        public void Clear() => _signalMap.Clear();
    
        public TData Filter(TData signal, int windowSize, Func<TData, double> observation)
        {
            var output = signal;
            lock (_lock)
            {
                // add data to signal map...
                _signalMap.Push(signal);
                var observations = _signalMap.Select(e => observation(e)).Take(windowSize);
                var mad = MedianAbsoluteDeviation.Estimate(observations);
                if (mad.IsOutlier(observation(output)))
                    output = default(TData);
                if (_signalMap.Count > windowSize)
                    _signalMap = new Stack<TData>(_signalMap.Take(windowSize).Reverse());
            }
            return output;
        }

        public double Average(TData signal, int window, Func<TData, double> signalIn)
        {
            _signalMap.Push(signal);
            var signalWindow = _signalMap.Take(window).Select(e => signalIn(e));
            return signalWindow.Average();
        }

    }

}
