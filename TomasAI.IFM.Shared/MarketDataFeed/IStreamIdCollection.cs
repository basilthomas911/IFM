using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public interface IStreamIdCollection
    {
        int Count { get; }
        int Add(string contractId);
        void Remove(int streamId);
        bool Exists(int streamId);
        void Clear();

        int this[string contractId] { get; }
    }
}
