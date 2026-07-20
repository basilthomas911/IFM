using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface IOptionPricerSettings
    {
        int SpreadPaths { get; }
        int ImpliedVolatilityPaths { get; }
    }
}
