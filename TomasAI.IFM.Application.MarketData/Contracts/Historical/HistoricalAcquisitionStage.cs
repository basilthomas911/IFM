using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Identifies the stage durably completed by a historical acquisition.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum HistoricalAcquisitionStage : byte
{
    /// <summary>No stage has completed.</summary>
    None = 0,
    /// <summary>The estimate was approved.</summary>
    Estimated = 1,
    /// <summary>The provider job was submitted or resumed.</summary>
    Submitted = 2,
    /// <summary>All provider files were downloaded and verified.</summary>
    Downloaded = 3,
    /// <summary>All records were normalized and accepted by the sink.</summary>
    Normalized = 4,
    /// <summary>The manifest was finalized.</summary>
    Completed = 5
}
