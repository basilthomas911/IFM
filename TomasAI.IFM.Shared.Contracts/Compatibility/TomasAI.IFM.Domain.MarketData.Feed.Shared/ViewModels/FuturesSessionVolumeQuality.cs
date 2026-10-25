using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Provider-neutral price and volume snapshot for one futures trading session.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
public enum FuturesSessionVolumeQuality : byte
{
    Unknown = 0,
    Bootstrapping = 1,
    ObservedComplete = 2,
    OfficialFinal = 3
}
