using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface IOptionSpreadPricer : IDisposable
    {
        (ICollection<OptionSpreadResult> PutSpreadResult, ICollection<OptionSpreadResult> CallSpreadResult, double Duration) PriceIronCondor(CreditSpreadPricerArgs pcsArgs, CreditSpreadPricerArgs ccsArgs);
        void Reset();
    }
    
}
