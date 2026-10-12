using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Trusted parent control message derived from committed ownership; business owners have no lease TTL.</summary>
[MessagePackObject]
public sealed record WorkerOptionChainOwnership(
    [property: Key(0)] long Revision,
    [property: Key(1)] string AuthorityScope,
    [property: Key(2)] ImmutableArray<WorkerOptionChainOwner> Owners,
    [property: Key(3)] string ContractSetDigest);
