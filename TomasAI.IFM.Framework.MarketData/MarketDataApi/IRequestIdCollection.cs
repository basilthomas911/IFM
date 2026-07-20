using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public interface IRequestIdCollection
    {
        int Count { get; }
        int Add(string contractId);
        void Remove(int streamId);
        bool Exists(int streamId);
        void Clear();
        void SetErrorHandler(Action<int, string> errorHandler);

        int this[string contractId] { get; }
    }
}
