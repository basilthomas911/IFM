namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;

/// <summary>Contains a fenced VWAP recovery transition, duplicate acknowledgement, or rejection.</summary>
public sealed record FuturesVwapRecoveryChange(FuturesVwapAccumulatorResult? FuturesVwapTransition,
    string? RejectionReason)
{
    /// <summary>Gets whether the recovery batch is acceptable for this generation and ordinal.</summary>
    public bool Accepted => RejectionReason is null;
}
