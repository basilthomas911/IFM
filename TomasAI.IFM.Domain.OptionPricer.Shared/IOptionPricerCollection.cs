using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserves the existing externally referenced type identity.")]
    public interface IOptionPricerCollection : IDisposable
    {
        IOptionPricer this[int optionPricerId] { get; }
        int Count { get; }
        IOptionPricerCollection GetByOptionType(OptionType optionType);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing externally referenced identity and named API contract.")]
        IOptionPricer Next();
        void Add(IOptionPricer optionPricer);
        void Remove(int deviceId);
        void Clear();
        bool Exists(OptionPricerId optionPricerId);
        void Release(IOptionPricer optionPricer);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        List<IOptionPricer> ToList();
    }
}
