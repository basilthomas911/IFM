using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation;

/// <summary>Accumulates replay and accepted live ES trades on the tick service's ordered writer.</summary>
internal sealed class FuturesVwapSourceAccumulator
{
    public bool IsReplayComplete => replayComplete;
    public long EligibleTradeCount => eligibleCount;
    private Guid replayGenerationId;
    private Guid liveEpochId;
    private long lastTradeOrdinal;
    private long lastSourceSequence;
    private DateTimeOffset asOfUtc;
    private decimal priceVolume;
    private long volume;
    private long eligibleCount;
    private long rejectedCount;
    private decimal lastPrice;
    private bool valid = true;
    private bool replayComplete;
    private FuturesVwapSourceInvalidReason invalidReason;

    /// <summary>Resets accumulated state for a fresh feed or session generation.</summary>
    /// <param name="generationId">The identifier of the feed generation being initialized.</param>
    public void Reset(Guid generationId)
    {
        replayGenerationId = generationId;
        liveEpochId = Guid.Empty;
        lastTradeOrdinal = lastSourceSequence = eligibleCount = rejectedCount = volume = 0;
        asOfUtc = default;
        priceVolume = lastPrice = 0;
        valid = true;
        invalidReason = FuturesVwapSourceInvalidReason.None;
        replayComplete = false;
    }

    /// <summary>Accumulates a replay trade into the VWAP source checkpoint.</summary>
    /// <param name="price">The trade or statistics price.</param>
    /// <param name="size">The trade quantity.</param>
    /// <param name="sequence">The provider record sequence number.</param>
    /// <param name="timestamp">The record timestamp used to determine replay continuity.</param>
    /// <param name="action">The trade action identifying a new trade or correction.</param>
    /// <param name="conditions">The normalized trade condition flags.</param>
    public void ApplyReplay(decimal price, long size, long sequence, DateTimeOffset timestamp,
        NormalizedTradeAction action, NormalizedTradeConditionFlags conditions)
        => Apply(price, size, sequence, timestamp, action, conditions);

    /// <summary>Marks VWAP replay complete and establishes the live stream epoch.</summary>
    /// <param name="streamEpochId">The identifier of the live stream continuity epoch.</param>
    public void CompleteReplay(Guid streamEpochId)
    {
        liveEpochId = streamEpochId;
        lastTradeOrdinal = 0;
        replayComplete = true;
    }

    /// <summary>Accumulates a live VWAP trade, invalidating continuity when the stream epoch or trade ordinal is inconsistent.</summary>
    /// <param name="price">The trade or statistics price.</param>
    /// <param name="size">The trade quantity.</param>
    /// <param name="sequence">The provider record sequence number.</param>
    /// <param name="timestamp">The record timestamp used to determine replay continuity.</param>
    /// <param name="action">The trade action identifying a new trade or correction.</param>
    /// <param name="conditions">The normalized trade condition flags.</param>
    /// <param name="streamEpochId">The identifier of the live stream continuity epoch.</param>
    /// <param name="tradeOrdinal">The consecutive trade ordinal within the live stream epoch.</param>
    public void ApplyLive(decimal price, long size, long sequence, DateTimeOffset timestamp,
        NormalizedTradeAction action, NormalizedTradeConditionFlags conditions,
        Guid streamEpochId, long tradeOrdinal)
    {
        if (!replayComplete)
            Invalidate(FuturesVwapSourceInvalidReason.RecoveryIncomplete);
        else if (streamEpochId != liveEpochId)
            Invalidate(FuturesVwapSourceInvalidReason.StreamEpochChanged);
        else if (tradeOrdinal != lastTradeOrdinal + 1)
            Invalidate(FuturesVwapSourceInvalidReason.DeliveryGap);
        liveEpochId = streamEpochId;
        lastTradeOrdinal = tradeOrdinal;
        Apply(price, size, sequence, timestamp, action, conditions);
    }

    private void Apply(decimal price, long size, long sequence, DateTimeOffset timestamp,
        NormalizedTradeAction action, NormalizedTradeConditionFlags conditions)
    {
        asOfUtc = timestamp.ToUniversalTime();
        lastSourceSequence = sequence;
        if (price > 0) lastPrice = price;
        if (price <= 0 || size <= 0 || action != NormalizedTradeAction.New
            || conditions.HasFlag(NormalizedTradeConditionFlags.Snapshot)
            || conditions.HasFlag(NormalizedTradeConditionFlags.UndefinedPrice))
        {
            rejectedCount = checked(rejectedCount + 1);
            Invalidate(action is NormalizedTradeAction.Change or NormalizedTradeAction.Cancel
                    or NormalizedTradeAction.Correct or NormalizedTradeAction.Clear
                ? FuturesVwapSourceInvalidReason.UncorrelatableCorrection
                : FuturesVwapSourceInvalidReason.InvalidTrade);
            return;
        }
        priceVolume = checked(priceVolume + price * size);
        volume = checked(volume + size);
        eligibleCount = checked(eligibleCount + 1);
    }

    private void Invalidate(FuturesVwapSourceInvalidReason reason)
    {
        valid = false;
        if (invalidReason == FuturesVwapSourceInvalidReason.None)
            invalidReason = reason;
    }

    /// <summary>Captures the accumulated VWAP source checkpoint and continuity status.</summary>
    /// <returns>The snapshot result.</returns>
    public FuturesVwapSourceCheckpoint Snapshot() => new()
    {
        StreamEpochId = liveEpochId,
        LastTradeOrdinal = lastTradeOrdinal,
        LastTradeSourceSequence = lastSourceSequence,
        AsOfUtc = asOfUtc,
        CumulativePriceVolume = priceVolume,
        CumulativeVolume = volume,
        EligibleTradeCount = eligibleCount,
        RejectedTradeCount = rejectedCount,
        LastPrice = lastPrice,
        IsValid = valid,
        IsReplayComplete = replayComplete,
        RecoveryGenerationId = replayGenerationId,
        InvalidReason = invalidReason
    };
}
