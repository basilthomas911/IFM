using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface IOptionPricerCollection : IDisposable
    {
        IOptionPricer this[int optionPricerId] { get; }
        int Count { get; }
        IOptionPricerCollection GetByOptionType(OptionType optionType);
        IOptionPricer Next();
        void Add(IOptionPricer optionPricer);
        void Remove(int deviceId);
        void Clear();
        bool Exists(OptionPricerId optionPricerId);
        void Release(IOptionPricer optionPricer);
        List<IOptionPricer> ToList();
    }
}
