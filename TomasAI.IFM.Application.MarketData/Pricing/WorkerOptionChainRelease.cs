using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record WorkerOptionChainRelease(
    [property: Key(0)] string ScopeId,
    [property: Key(1)] Guid LeaseId,
    [property: Key(2)] Guid GenerationId,
    [property: Key(3)] WorkerOptionChainOwnership? Ownership = null);
