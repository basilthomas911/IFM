using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class OptionPricerSettings : IOptionPricerSettings
    {
        private readonly int _spreadPaths;
        private readonly int _impliedVolPaths;

        public int SpreadPaths => _spreadPaths;
        public int ImpliedVolatilityPaths => _impliedVolPaths;

        public OptionPricerSettings(int spreadPaths, int impliedVolPaths)
        {
            _spreadPaths = spreadPaths;
            _impliedVolPaths = impliedVolPaths;
        }
    }
}
