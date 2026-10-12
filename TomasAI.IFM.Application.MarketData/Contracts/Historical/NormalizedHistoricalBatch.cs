using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Provides one bounded normalized sink batch.</summary>
public sealed record NormalizedHistoricalBatch(
    Guid DataLoadAttemptId,
    string ProviderFileId,
    long BatchOrdinal,
    string SourcePosition,
    IReadOnlyList<FuturesTradeSessionBarReadModel> Observations,
    IReadOnlyList<NormalizedHistoricalTrade> Trades,
    string NormalizedSha256,
    bool IsFinal);
