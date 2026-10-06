using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;

/// <summary>Contains a pure Wilder ATR transition, duplicate decision, or business rejection.</summary>
public sealed record FuturesAtrSignalChange(FuturesAtrSignalReadModel? FuturesAtrSignal,
    FuturesAtrAccumulatorCheckpoint? FuturesAtrCheckpoint, bool HasChanged, string? RejectionReason)
{
    /// <summary>Gets whether the observation passed the domain guards.</summary>
    public bool Accepted => RejectionReason is null;
}
