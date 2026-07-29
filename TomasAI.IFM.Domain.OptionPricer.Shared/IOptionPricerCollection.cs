using TomasAI.IFM.Shared.Trade;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
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
