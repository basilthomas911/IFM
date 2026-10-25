using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record OptionIvMetricRevision(
    OptionIvMetricSnapshot Snapshot,
    int Revision,
    string? SupersedesSnapshotId,
    long PublicationSequence);
