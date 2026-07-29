using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public interface IOptionSpreadPricer : IDisposable
    {
        (ICollection<OptionSpreadResult> PutSpreadResult, ICollection<OptionSpreadResult> CallSpreadResult, double Duration) PriceIronCondor(CreditSpreadPricerArgs pcsArgs, CreditSpreadPricerArgs ccsArgs);
        void Reset();
    }
    
}
