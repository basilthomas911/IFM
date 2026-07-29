using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public interface IOptionPricerDeviceCollection : ICollection<OptionPricerDeviceReadModel>
    {
    }
}
