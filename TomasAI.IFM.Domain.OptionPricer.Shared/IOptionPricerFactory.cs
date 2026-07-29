using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public interface IOptionPricerFactory
    {
        int DeviceCount { get; }

        IOptionPricerCollection? GetPricers(OptionStyle optionStyle, OptionType optionType, int daysToMaturity);
        IOptionPricerCollection? GetPricersOne(OptionStyle optionStyle, OptionType optionType, int daysToMaturity);
        void Clear();
    }
}
