using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface ISpreadPricerCollection : IDisposable
    {
        ISpreadPricer this[int optionPricerId] { get; }
        int Count { get; }
        ISpreadPricer Next();
        void Add(ISpreadPricer optionPricer);
        void Remove(int deviceId);
        void Clear();
        bool Exists(OptionPricerId optionPricerId);
        void Release(ISpreadPricer optionPricer);
        List<ISpreadPricer> ToList();
    }
}
