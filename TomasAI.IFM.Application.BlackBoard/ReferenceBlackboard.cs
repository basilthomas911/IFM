using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard;

internal sealed class ReferenceBlackboard(
    IRedisCache redisCache,
    IJsonSerializer jsonSerializer) : IReferenceBlackboard
{
    public ReferenceLookupCacheModel ReferenceLookup { get; } =
        new(redisCache, jsonSerializer);
}
