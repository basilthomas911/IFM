using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.OptionPricer.Model;

namespace TomasAI.IFM.UnitTests.Validation
{
    [TestClass]
    public class OptionPricerValidationTests
    {
        [TestMethod]
        public void OptionPricerDeviceOk()
        {
            var device = new OptionPricerDevice(
                0,
                "NVidia Titan Xp",
                20,
                16,
                2000,
                OptionType.Put,
                true);
        }

        [TestMethod]
        public void OptionPricerDeviceWithOutOfRangeLocationDeviceId()
        {
            try
            {
                var device = new OptionPricerDevice(
                    -1,
                    "NVidia Titan Xp",
                    20,
                    16,
                    2000,
                    OptionType.Put,
                    true);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "OptionPricerDevice.LocalDeviceId is out of range");
                return;
            }
            Assert.Fail("OptionPricerDevice.LocationDeviceId is out of range");
        }

        [TestMethod]
        public void OptionPricerDeviceWithEmptyDeviceName()
        {
            try
            {
                var device = new OptionPricerDevice(
                    0,
                    null,
                    20,
                    16,
                    2000,
                    OptionType.Put,
                    true);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "OptionPricerDevice.DeviceName is empty");
                return;
            }
            Assert.Fail("OptionPricerDevice.DeviceName is empty");
        }

        [TestMethod]
        public void OptionPricerDeviceWithZeroSpreadPaths()
        {
            try
            {
                var device = new OptionPricerDevice(
                    0,
                    "NVidia Titan Xp",
                    0,
                    16,
                    2000,
                    OptionType.Put,
                    true);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "OptionPricerDevice.SpreadPaths must be greater than zero");
                return;
            }
            Assert.Fail("OptionPricerDevice.SpreadPaths must be greater than zero");
        }

        [TestMethod]
        public void OptionPricerDeviceWithZeroVolatilityPaths()
        {
            try
            {
                var device = new OptionPricerDevice(
                    0,
                    "NVidia Titan Xp",
                    19,
                    0,
                    2000,
                    OptionType.Put,
                    true);
            }
            catch (ValidationException ex)
            {
                Assert.IsNotNull(ex.ValidationErrors);
                Assert.IsTrue(ex.ValidationErrors.Length == 1);
                Assert.AreEqual(ex.ValidationErrors[0].ErrorMessage, "OptionPricerDevice.VolatilityPaths must be greater than zero");
                return;
            }
            Assert.Fail("OptionPricerDevice.VolatilityPaths must be greater than zero");
        }
    }

}
