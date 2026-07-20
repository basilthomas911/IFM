using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class OptionGreeks
    {
        private readonly double _impliedVol;
        private readonly double _delta;
        private readonly double _gamma;
        private readonly double _theta;
        private readonly double _vega;
        private readonly double _rho;
        private readonly bool _success;

        public bool Success => _success;
        public double ImpliedVolatility => _impliedVol;
        public double Delta => _delta;
        public double Gamma => _gamma;
        public double Theta => _theta;
        public double Vega => _vega;
        public double Rho => _rho;

        public OptionGreeks(
            bool success,
            double impliedVol,
            double delta,
            double gamma,
            double theta,
            double vega,
            double rho)
        {
            _success = success;
            _impliedVol = impliedVol;
            _delta = delta;
            _gamma = gamma;
            _theta = theta;
            _vega = vega;
            _rho = rho;
        }
    }
}
