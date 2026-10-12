using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Application.MarketData.Pricing;

public sealed record TradeBlotterStagingResult(ImmutableArray<StagedTradeLeg> Legs, bool ManualReviewRequired,
    string Status, Guid SnapshotId, Guid GenerationId, DateTimeOffset EvaluatedAtUtc, string SnapshotDigest);
