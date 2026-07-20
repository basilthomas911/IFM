using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class ProbabilityValue
    {
        private double _value;

        public double Value => _value;
        public double Probability { get; set; }

        public ProbabilityValue(double value)
        {
            _value = value;
        }
        public ProbabilityValue(double value, double probability)
        {
            _value = value;
            Probability = probability;
        }

    }
}
