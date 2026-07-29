using TomasAI.IFM.Shared.Trade;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public interface ISpreadPricerFactory
    {
        ISpreadPricerCollection GetPricers(OptionStyle optionStyle, OptionType optionType, int daysToMaturity);
    }
}
