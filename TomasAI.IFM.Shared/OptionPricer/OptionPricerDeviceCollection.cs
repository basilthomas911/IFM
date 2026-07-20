using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class OptionPricerDeviceCollection : List<OptionPricerDeviceReadModel>, IOptionPricerDeviceCollection
    {
        public OptionPricerDeviceCollection(IOptionPricerQueryApi optionPricerQuery)
        {
            var serviceResult = optionPricerQuery.GetOptionPricerDevicesAsync().Result;
            if (!serviceResult.Success || serviceResult.Value is null)
                throw new InvalidOperationException("Unable to load Option Pricer Devices");
            this.AddRange(serviceResult.Value.Devices);
        }
    }
}
