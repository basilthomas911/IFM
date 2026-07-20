using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.FuturesContract.Model;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.UnitTests.Validation
{
    [TestClass]
    public class MarketDataValidationTests
    {
        [TestMethod]
        public void FuturesContractValidationOk()
        {
            var contract = new FuturesContract(
                "ES20180615",
                "E-mini S&P 500 ",
                "ES",
                "ESM8",
                "FUT",
                "USD",
                "GLOBEX",
                "50",
                new DateTime(2018, 06, 15), false);
        }

        [TestMethod]
        public void FuturesContractWithEmptyContractId()
        {
            try
            {
                var contract = new FuturesContract(
                    null,
                    "E-mini S&P 500 ",
                    "ES",
                    "ESM8",
                    "FUT",
                    "USD",
                    "GLOBEX",
                    "50",
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.ContractId is required");
                return;
            }
            Assert.Fail("FuturesContract.ContractId is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptyDescription()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    null,
                    "ES",
                    "ESM8",
                    "FUT",
                    "USD",
                    "GLOBEX",
                    "50",
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.Description is required");
                return;
            }
            Assert.Fail("FuturesContract.Description is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptySymbol()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    "E-mini S&P 500 ",
                    null,
                    "ESM8",
                    "FUT",
                    "USD",
                    "GLOBEX",
                    "50",
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.Symbol is required");
                return;
            }
            Assert.Fail("FuturesContract.Symbol is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptyLocalSymbol()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    "E-mini S&P 500 ",
                    "ES",
                    null,
                    "FUT",
                    "USD",
                    "GLOBEX",
                    "50",
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.LocalSymbol is required");
                return;
            }
            Assert.Fail("FuturesContract.LocalSymbol is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptySecurityType()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    "E-mini S&P 500 ",
                    "ES",
                    "ESM8",
                    null,
                    "USD",
                    "GLOBEX",
                    "50",
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.SecurityType is required");
                return;
            }
            Assert.Fail("FuturesContract.SecurityType is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptyCurrency()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    "E-mini S&P 500 ",
                    "ES",
                    "ESM8",
                    "FUT",
                    null,
                    "GLOBEX",
                    "50",
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.Currency is required");
                return;
            }
            Assert.Fail("FuturesContract.Currency is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptyExchange()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    "E-mini S&P 500 ",
                    "ES",
                    "ESM8",
                    "FUT",
                    "USD",
                    null,
                    "50",
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.Exchange is required");
                return;
            }
            Assert.Fail("FuturesContract.Exchange is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptyMultiplier()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    "E-mini S&P 500 ",
                    "ES",
                    "ESM8",
                    "FUT",
                    "USD",
                    "GLOBEX",
                    null,
                    new DateTime(2018, 06, 15), false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.Multiplier is required");
                return;
            }
            Assert.Fail("FuturesContract.Multiplier is required");
        }

        [TestMethod]
        public void FuturesContractWithEmptyLastTradeDate()
        {
            try
            {
                var contract = new FuturesContract(
                    "ES20180615",
                    "E-mini S&P 500 ",
                    "ES",
                    "ESM8",
                    "FUT",
                    "USD",
                    "GLOBEX",
                    "50",
                    DateTime.MinValue, false);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "FuturesContract.LastTradeDate is required");
                return;
            }
            Assert.Fail("FuturesContract.LastTradeDate is required");
        }
    }
}
