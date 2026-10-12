using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

/// <summary>Contains one Wilder RSI transition result.</summary>
public sealed record FuturesRsiWilderResult(
    FuturesRsiAccumulatorCheckpoint Checkpoint,
    decimal PriceChange,
    decimal PriceGain,
    decimal PriceLoss,
    decimal? AverageGain,
    decimal? AverageLoss,
    double? RelativeStrength,
    double? PreviousRsi,
    double? Slope,
    bool IsWarm,
    MarketObservationApplicationDisposition Disposition)
{
    /// <summary>Gets whether this transition advanced durable state.</summary>
    public bool IsApplied => Disposition == MarketObservationApplicationDisposition.Applied;

    /// <summary>Creates a no-op transition for a duplicate or older observation.</summary>
    public static FuturesRsiWilderResult Ignored(
        FuturesRsiAccumulatorCheckpoint checkpoint,
        MarketObservationApplicationDisposition disposition) 
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return new(checkpoint, 0m, 0m, 0m, checkpoint.AverageGain, checkpoint.AverageLoss, null,
            checkpoint.CurrentRsi, null, false, disposition);
    }
}
