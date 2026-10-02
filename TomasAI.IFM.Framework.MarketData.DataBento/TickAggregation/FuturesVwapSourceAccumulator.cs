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

    public void ApplyReplay(decimal price, long size, long sequence, DateTimeOffset timestamp,
        NormalizedTradeAction action, NormalizedTradeConditionFlags conditions)
        => Apply(price, size, sequence, timestamp, action, conditions);

    public void CompleteReplay(Guid streamEpochId)
    {
        liveEpochId = streamEpochId;
        lastTradeOrdinal = 0;
        replayComplete = true;
    }

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
