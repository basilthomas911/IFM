using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class OptionSpreadPricerFactory : IOptionSpreadPricerFactory
    {
        readonly IOptionPricerFactory _optionSpreadPricer;

        public OptionSpreadPricerFactory(IOptionPricerFactory optionPricerFactory)
        {
            _optionSpreadPricer = optionPricerFactory;
        }


        public IOptionSpreadPricer Create() => null;

    }
}
