using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>Identifies the market-data source that produced a durable snapshot.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketOutlookSnapshotSource : byte
{
    /// <summary>Legacy snapshot written before source provenance was captured.</summary>
    Unknown = 0,
    /// <summary>Snapshot derived from the Databento live feed.</summary>
    DatabentoLive = 1,
    /// <summary>Snapshot derived from the deterministic synthetic feed.</summary>
    Synthetic = 2
}
