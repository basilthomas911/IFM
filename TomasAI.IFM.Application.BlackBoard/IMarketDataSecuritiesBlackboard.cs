using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface IMarketDataSecuritiesBlackboard
{
    IDatabentoContractMappingCache DatabentoContractMapping { get; }
    FuturesContractCacheModel FuturesContract { get; }
    FuturesContractSymbolCacheModel FuturesContractSymbol { get; }
}
