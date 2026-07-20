using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface IOptionSpreadPricerFactory
    {
        IOptionSpreadPricer Create();
    }
}
