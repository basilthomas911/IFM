using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record OptionIvLatestPointer(
    VolatilityStorageScope Scope,
    string SnapshotId,
    string SnapshotDigest,
    long PublicationSequence,
    DateTimeOffset AvailableAtUtc);
