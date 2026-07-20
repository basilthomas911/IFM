using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface ICreditSpreadPricer : IDisposable
    {
        (ICollection<CreditSpreadResult> PutSpreadResult, ICollection<CreditSpreadResult> CallSpreadResult, double Duration) PriceIronCondor(CreditSpreadPricerArgs pcsArgs, CreditSpreadPricerArgs ccsArgs);
        void Reset();
    }
    
}
