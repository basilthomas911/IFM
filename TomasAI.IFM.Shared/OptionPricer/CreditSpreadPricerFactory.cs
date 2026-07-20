using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class CreditSpreadPricerFactory : ICreditSpreadPricerFactory
    {
        private readonly ICreditSpreadPricer _creditSpreadPricer;

        public CreditSpreadPricerFactory(ICreditSpreadPricer creditSpreadPricer)
            => _creditSpreadPricer = creditSpreadPricer;

        public CreditSpreadPricerFactory(ICreditSpreadWebPricer creditSpreadWebPricer)
            => _creditSpreadPricer = creditSpreadWebPricer;

        public ICreditSpreadPricer Create()
            => _creditSpreadPricer;

    }
}
