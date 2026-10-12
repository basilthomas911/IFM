using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IDbCacheName
    {
        string EntityTypeIdMap { get; }
        string EventTypeIdMap { get; }
        string FuturesEodDataMap { get; }
        string FutureOptionTickDataMap { get; }
    }

}
