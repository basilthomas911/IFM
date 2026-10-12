using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Identifies the durable data load lifecycle.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum HistoricalDataLoaderStatus : byte
{
    /// <summary>The attempt has not started.</summary>
    None,
    /// <summary>The attempt is running or resumable.</summary>
    Processing,
    /// <summary>The attempt completed and its manifest is immutable.</summary>
    Completed,
    /// <summary>The attempt failed at its recorded checkpoint.</summary>
    Failed
}
