using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Immutable market request and its accepted evidence; never refreshed on replay.</summary>
[MessagePackObject]
public sealed record CompositionPreparation([property: Key(0)] int SchemaVersion,
    [property: Key(1)] CompositionPreparationKey Key, [property: Key(2)] string Dataset,
    [property: Key(3)] CompositionSnapshotRequest Request, [property: Key(4)] MarketCompositionSnapshot Snapshot,
    [property: Key(5)] DateTimeOffset PreparedAtUtc, [property: Key(6)] string Digest,
    [property: Key(7)] WorkerOptionChainRelease? DiscoveryLease = null);
