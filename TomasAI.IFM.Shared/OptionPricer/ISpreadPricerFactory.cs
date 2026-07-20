using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface ISpreadPricerFactory
    {
        ISpreadPricerCollection GetPricers(OptionStyle optionStyle, OptionType optionType, int daysToMaturity);
    }
}
