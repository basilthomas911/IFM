using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class SpreadProbability
    {
        private readonly double _forwardPrice;
        private readonly double _lossProbability;
        private readonly decimal _lossThreshold;
        private readonly int _lossThresholdCount;

        public SpreadProbability(double forwardPrice, double lossProbability, decimal lossThreshold, int lossThresholdCount)
        {
            _forwardPrice = forwardPrice;
            _lossProbability = lossProbability;
            _lossThreshold = lossThreshold;
            _lossThresholdCount = lossThresholdCount;
        }

        public double ForwardPrice => _forwardPrice;
        public double LossProbability => _lossProbability;
        public decimal LossThreshold => _lossThreshold;
        public int LossThresholdCount => _lossThresholdCount;
    }
}
