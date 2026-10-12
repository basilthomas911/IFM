using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

public interface IMarketDataBlackboard
{
    RiskFreeRateCacheModel RiskFreeRate { get; }
}
