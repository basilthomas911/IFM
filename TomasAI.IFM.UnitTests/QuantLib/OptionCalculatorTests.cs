using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.UnitTests.QuantLib
{
    [TestClass]
    public class OptionCalculatorTests
    {
        [TestMethod]
        public void CalculateImpliedVolatilityOk()
        {
            var valueDate = new DateTime(2018, 10, 26);
            var maturityDate = new DateTime(2018, 10, 26);
            var vc = new OptionCalculator(valueDate, maturityDate);
            var optionGreeks = vc.GetOptionGreeks(OptionTypeName.Put, 2760.50, 2630, 16.625, 0.0214);
            Assert.IsTrue(optionGreeks.Success);
        }
    }
}
