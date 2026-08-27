using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;

/// <summary>Advances exact, replayable futures-session price-volume state.</summary>
public static class FuturesVwapAccumulator
{
    /// <summary>Applies one live trade while enforcing epoch and ordinal continuity.</summary>
    public static FuturesVwapAccumulatorResult ApplyLive(
        FuturesVwapSignalEntityId entityId,
        FuturesVwapCheckpoint? checkpoint,
        FuturesVwapTradeObservation trade,
        FuturesVwapConfiguration configuration)
    {
        Validate(entityId, trade, configuration);
        checkpoint ??= new FuturesVwapCheckpoint
        {
            SessionStartUtc = trade.SessionStartUtc,
            SessionEndUtc = trade.SessionEndUtc,
            IsValid = true
        };
        if (checkpoint.IsRecovering)
            return Invalidate(entityId, checkpoint, trade,
                FuturesVwapInvalidReason.RecoveryIncomplete, configuration);
        if (checkpoint.StreamEpochId != Guid.Empty
            && checkpoint.StreamEpochId == trade.StreamEpochId
            && trade.TradeOrdinal <= checkpoint.LastTradeOrdinal)
            return new(checkpoint, BuildSignal(entityId, checkpoint, configuration), false);
        if (!IsEligible(trade, allowReplay: false))
            return Invalidate(entityId, checkpoint, trade,
                trade.Action is FuturesVwapTradeAction.Change or FuturesVwapTradeAction.Cancel
                    or FuturesVwapTradeAction.Correct or FuturesVwapTradeAction.Clear
                    ? FuturesVwapInvalidReason.UncorrelatableCorrection
                    : FuturesVwapInvalidReason.InvalidTrade,
                configuration);
        if (checkpoint.StreamEpochId != Guid.Empty && checkpoint.StreamEpochId != trade.StreamEpochId)
            return Invalidate(entityId, checkpoint, trade,
                FuturesVwapInvalidReason.StreamEpochChanged, configuration);
        if (checkpoint.LastTradeOrdinal > 0 && trade.TradeOrdinal != checkpoint.LastTradeOrdinal + 1)
            return Invalidate(entityId, checkpoint, trade,
                FuturesVwapInvalidReason.DeliveryGap, configuration, includeContribution: true);
        var next = Include(checkpoint, trade) with
        {
            StreamEpochId = trade.StreamEpochId,
            LastTradeOrdinal = trade.TradeOrdinal,
            IsValid = checkpoint.IsValid,
            InvalidReason = checkpoint.InvalidReason
        };
        return new(next, BuildSignal(entityId, next, configuration), true);
    }

    /// <summary>Applies one bounded ordered private exact-trade recovery batch.</summary>
    public static FuturesVwapAccumulatorResult ApplyRecovery(
        FuturesVwapSignalEntityId entityId,
        FuturesVwapCheckpoint? checkpoint,
        Guid recoveryGenerationId,
        long batchOrdinal,
        bool isFirstBatch,
        bool isFinalBatch,
        IReadOnlyCollection<FuturesVwapTradeObservation> trades,
        FuturesVwapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(trades);
        if (recoveryGenerationId == Guid.Empty) throw new ArgumentException(
            "A recovery generation is required.", nameof(recoveryGenerationId));
        if (batchOrdinal < 0 || trades.Count > 4096)
            throw new ArgumentOutOfRangeException(nameof(batchOrdinal));
        var ordered = trades.OrderBy(value => value.EventTimestampUtc)
            .ThenBy(value => value.SourceSequence).ToArray();
        if (isFirstBatch)
        {
            checkpoint = new FuturesVwapCheckpoint
            {
                SessionStartUtc = ordered.FirstOrDefault()?.SessionStartUtc ?? default,
                SessionEndUtc = ordered.FirstOrDefault()?.SessionEndUtc ?? default,
                IsValid = true,
                IsRecovering = true,
                InvalidReason = FuturesVwapInvalidReason.RecoveryIncomplete,
                RecoveryGenerationId = recoveryGenerationId,
                RecoveryBatchOrdinal = -1,
                StreamEpochId = recoveryGenerationId
            };
        }
        if (checkpoint is null)
            throw new InvalidOperationException("Recovery must start with its first batch.");
        if (checkpoint.RecoveryGenerationId != recoveryGenerationId)
            throw new InvalidOperationException("Recovery generation does not match active VWAP state.");
        if (batchOrdinal <= checkpoint.RecoveryBatchOrdinal)
            return new(checkpoint, BuildSignal(entityId, checkpoint, configuration), false);
        if (batchOrdinal != checkpoint.RecoveryBatchOrdinal + 1)
            throw new InvalidOperationException("VWAP recovery batches must be contiguous.");

        var next = checkpoint;
        foreach (var trade in ordered)
        {
            Validate(entityId, trade, configuration, requireLiveLineage: false);
            if (!IsEligible(trade, allowReplay: true))
            {
                next = next with
                {
                    RejectedTradeCount = checked(next.RejectedTradeCount + 1),
                    IsValid = false,
                    InvalidReason = FuturesVwapInvalidReason.UncorrelatableCorrection,
                    AsOfUtc = trade.EventTimestampUtc
                };
                continue;
            }
            next = Include(next, trade) with
            {
                StreamEpochId = recoveryGenerationId,
                LastTradeOrdinal = next.EligibleTradeCount
            };
        }
        next = next with
        {
            RecoveryBatchOrdinal = batchOrdinal,
            IsRecovering = !isFinalBatch,
            IsValid = isFinalBatch && next.InvalidReason is FuturesVwapInvalidReason.None
                or FuturesVwapInvalidReason.RecoveryIncomplete,
            InvalidReason = isFinalBatch && next.InvalidReason == FuturesVwapInvalidReason.RecoveryIncomplete
                ? FuturesVwapInvalidReason.None : next.InvalidReason
        };
        if (!isFinalBatch)
            next = next with { IsValid = false, InvalidReason = FuturesVwapInvalidReason.RecoveryIncomplete };
        return new(next, BuildSignal(entityId, next, configuration), true);
    }

    static FuturesVwapAccumulatorResult Invalidate(
        FuturesVwapSignalEntityId entityId,
        FuturesVwapCheckpoint checkpoint,
        FuturesVwapTradeObservation trade,
        FuturesVwapInvalidReason reason,
        FuturesVwapConfiguration configuration,
        bool includeContribution = false)
    {
        var next = includeContribution && IsPositive(trade) ? Include(checkpoint, trade) : checkpoint;
        next = next with
        {
            SessionStartUtc = trade.SessionStartUtc,
            SessionEndUtc = trade.SessionEndUtc,
            LastPrice = trade.Price > 0 ? trade.Price : next.LastPrice,
            LastTradeSourceSequence = Math.Max(next.LastTradeSourceSequence, trade.SourceSequence),
            StreamEpochId = trade.StreamEpochId,
            LastTradeOrdinal = Math.Max(next.LastTradeOrdinal, trade.TradeOrdinal),
            RejectedTradeCount = checked(next.RejectedTradeCount + 1),
            IsValid = false,
            InvalidReason = reason,
            AsOfUtc = trade.EventTimestampUtc
        };
        return new(next, BuildSignal(entityId, next, configuration), true);
    }

    static FuturesVwapCheckpoint Include(FuturesVwapCheckpoint checkpoint, FuturesVwapTradeObservation trade) =>
        checkpoint with
        {
            SessionStartUtc = trade.SessionStartUtc,
            SessionEndUtc = trade.SessionEndUtc,
            CumulativePriceVolume = checked(checkpoint.CumulativePriceVolume + trade.Price * trade.Size),
            CumulativeVolume = checked(checkpoint.CumulativeVolume + trade.Size),
            EligibleTradeCount = checked(checkpoint.EligibleTradeCount + 1),
            LastPrice = trade.Price,
            LastTradeSourceSequence = trade.SourceSequence,
            AsOfUtc = trade.EventTimestampUtc
        };

    static FuturesVwapSignalReadModel BuildSignal(
        FuturesVwapSignalEntityId entityId,
        FuturesVwapCheckpoint checkpoint,
        FuturesVwapConfiguration configuration)
    {
        decimal? vwap = checkpoint.CumulativeVolume > 0
            ? checkpoint.CumulativePriceVolume / checkpoint.CumulativeVolume : null;
        var warm = vwap is not null && checkpoint.EligibleTradeCount > 0 && !checkpoint.IsRecovering;
        return new FuturesVwapSignalReadModel
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            ConfigurationId = configuration.ConfigurationId,
            SessionStartUtc = checkpoint.SessionStartUtc,
            SessionEndUtc = checkpoint.SessionEndUtc,
            AsOfUtc = checkpoint.AsOfUtc,
            CumulativePriceVolume = checkpoint.CumulativePriceVolume,
            CumulativeVolume = checkpoint.CumulativeVolume,
            EligibleTradeCount = checkpoint.EligibleTradeCount,
            RejectedTradeCount = checkpoint.RejectedTradeCount,
            LastPrice = checkpoint.LastPrice,
            Vwap = vwap,
            PriceMinusVwap = vwap is null ? null : checkpoint.LastPrice - vwap,
            PriceToVwapPercent = vwap is null or 0 ? null : checkpoint.LastPrice / vwap - 1m,
            LastTradeSourceSequence = checkpoint.LastTradeSourceSequence,
            StreamEpochId = checkpoint.StreamEpochId,
            LastTradeOrdinal = checkpoint.LastTradeOrdinal,
            IsWarm = warm,
            IsValid = warm && checkpoint.IsValid,
            InvalidReason = checkpoint.InvalidReason,
            IsTickExact = warm && checkpoint.IsValid,
            CalculationMethod = FuturesVwapCalculationMethod.TickExact
        };
    }

    static bool IsEligible(FuturesVwapTradeObservation trade, bool allowReplay) => IsPositive(trade)
        && trade.Action == FuturesVwapTradeAction.New
        && !trade.Conditions.HasFlag(FuturesVwapTradeConditionFlags.UndefinedPrice)
        && !trade.Conditions.HasFlag(FuturesVwapTradeConditionFlags.Snapshot)
        && (allowReplay || !trade.Conditions.HasFlag(FuturesVwapTradeConditionFlags.Replay));

    static bool IsPositive(FuturesVwapTradeObservation trade) => trade.Price > 0 && trade.Size > 0;

    static void Validate(
        FuturesVwapSignalEntityId entityId,
        FuturesVwapTradeObservation trade,
        FuturesVwapConfiguration configuration,
        bool requireLiveLineage = true)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(configuration);
        if (new FuturesVwapSignalEntityIdValidationRules().Execute(entityId).Length != 0)
            throw new ArgumentException("A valid VWAP entity identity is required.", nameof(entityId));
        if (!string.Equals(entityId.ContractId, trade.ContractId, StringComparison.Ordinal)
            || entityId.ValueDate != trade.ValueDate
            || !string.Equals(entityId.ConfigurationId, configuration.ConfigurationId, StringComparison.Ordinal))
            throw new InvalidOperationException("VWAP trade identity does not match its command stream.");
        if (trade.EventTimestampUtc.Offset != TimeSpan.Zero
            || trade.SessionStartUtc.Offset != TimeSpan.Zero
            || trade.SessionEndUtc.Offset != TimeSpan.Zero
            || trade.SessionStartUtc >= trade.SessionEndUtc
            || trade.EventTimestampUtc < trade.SessionStartUtc
            || trade.EventTimestampUtc > trade.SessionEndUtc)
            throw new InvalidOperationException("VWAP trade session timestamps are invalid.");
        if (requireLiveLineage && (trade.StreamEpochId == Guid.Empty || trade.TradeOrdinal <= 0))
            throw new InvalidOperationException("Live VWAP trade lineage is incomplete.");
    }
}

/// <summary>Contains one VWAP state transition and its projected signal.</summary>
public sealed record FuturesVwapAccumulatorResult(
    FuturesVwapCheckpoint Checkpoint,
    FuturesVwapSignalReadModel Signal,
    bool Changed);
