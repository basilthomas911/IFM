using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Provides the durable resume position for acquisition redelivery.</summary>
public sealed record HistoricalAcquisitionCheckpoint
{
    /// <summary>Gets the data load attempt identity.</summary>
    public required Guid DataLoadAttemptId { get; init; }
    /// <summary>Gets the last durably completed stage.</summary>
    public HistoricalAcquisitionStage Stage { get; init; }
    /// <summary>Gets the existing provider job identity, when submitted.</summary>
    public string ProviderJobId { get; init; } = string.Empty;
    /// <summary>Gets the last accepted file identity.</summary>
    public string ProviderFileId { get; init; } = string.Empty;
    /// <summary>Gets the last accepted bounded batch ordinal.</summary>
    public long BatchOrdinal { get; init; } = -1;
    /// <summary>Gets the provider reader resume position.</summary>
    public string SourcePosition { get; init; } = string.Empty;
}
