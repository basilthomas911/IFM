using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public class OptionPricerDeviceCollection : List<OptionPricerDeviceReadModel>, IOptionPricerDeviceCollection
    {
        public OptionPricerDeviceCollection(IEnumerable<OptionPricerDeviceReadModel> devices)
        {
            AddRange(devices);
        }

        public static async Task<OptionPricerDeviceCollection> CreateAsync(
            IOptionPricerQueryApi optionPricerQuery,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(optionPricerQuery);
            var serviceResult = await optionPricerQuery.GetOptionPricerDevicesAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!serviceResult.Success || serviceResult.Value is null)
                throw new InvalidOperationException("Unable to load Option Pricer Devices");
            return new OptionPricerDeviceCollection(serviceResult.Value.Devices);
        }
    }
}
