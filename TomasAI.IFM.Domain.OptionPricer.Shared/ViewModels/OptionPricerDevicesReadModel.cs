using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels
{
    public class OptionPricerDevicesReadModel
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
        public OptionPricerDeviceReadModel[] Devices { get; set; }
    }
}
