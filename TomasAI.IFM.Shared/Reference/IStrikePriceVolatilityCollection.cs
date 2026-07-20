using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Reference
{
    public interface IStrikePriceVolatilityCollection
    {
        int Count { get; }
        IStrikePriceVolatility this[StrikePriceVolatilityId id] { get; }

        bool Exists(StrikePriceVolatilityId id);
        void Add(IStrikePriceVolatility item);
        void Clear();
        void Remove(StrikePriceVolatilityId id);
        
    }
}
