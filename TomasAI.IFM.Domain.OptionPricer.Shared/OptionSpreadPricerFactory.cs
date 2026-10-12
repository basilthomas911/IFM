using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public class OptionSpreadPricerFactory : IOptionSpreadPricerFactory
    {

        public OptionSpreadPricerFactory(IOptionPricerFactory optionPricerFactory)
        {
            ArgumentNullException.ThrowIfNull(optionPricerFactory);
        }


        public IOptionSpreadPricer Create() => null;

    }
}
