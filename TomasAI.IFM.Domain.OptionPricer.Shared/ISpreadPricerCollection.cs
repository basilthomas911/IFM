using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserves the existing externally referenced type identity.")]
    public interface ISpreadPricerCollection : IDisposable
    {
        ISpreadPricer this[int optionPricerId] { get; }
        int Count { get; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing externally referenced identity and named API contract.")]
        ISpreadPricer Next();
        void Add(ISpreadPricer optionPricer);
        void Remove(int deviceId);
        void Clear();
        bool Exists(OptionPricerId optionPricerId);
        void Release(ISpreadPricer optionPricer);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        List<ISpreadPricer> ToList();
    }
}
