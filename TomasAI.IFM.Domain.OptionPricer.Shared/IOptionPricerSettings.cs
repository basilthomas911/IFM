using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public interface IOptionPricerSettings
    {
        int SpreadPaths { get; }
        int ImpliedVolatilityPaths { get; }
    }
}
