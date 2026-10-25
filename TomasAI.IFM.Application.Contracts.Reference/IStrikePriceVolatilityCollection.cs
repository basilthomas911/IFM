using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Reference.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserve the published domain collection type name used by consumers.")]
    public interface IStrikePriceVolatilityCollection
    {
        int Count { get; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1043", Justification = "The domain indexer uses its typed contract identity rather than a scalar string key.")]
        IStrikePriceVolatility this[StrikePriceVolatilityId id] { get; }

        bool Exists(StrikePriceVolatilityId id);
        void Add(IStrikePriceVolatility item);
        void Clear();
        void Remove(StrikePriceVolatilityId id);

    }
}
