using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record WorkerOptionChainOwner(
    [property: Key(0)] Guid LeaseId,
    [property: Key(1)] ImmutableArray<string> ContractIds);
