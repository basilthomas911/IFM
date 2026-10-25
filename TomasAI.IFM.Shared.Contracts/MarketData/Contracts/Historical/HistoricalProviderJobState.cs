using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Historical;

/// <summary>Identifies the lifecycle state of a provider batch job.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published provider enum wire representation.")]
public enum HistoricalProviderJobState : byte
{
    /// <summary>The state is unknown.</summary>
    Unknown = 0,
    /// <summary>The job is queued.</summary>
    Queued = 1,
    /// <summary>The provider is processing the job.</summary>
    Processing = 2,
    /// <summary>The job completed and its files are available.</summary>
    Completed = 3,
    /// <summary>The job failed.</summary>
    Failed = 4,
    /// <summary>The job expired.</summary>
    Expired = 5
}
