using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Model;

/// <summary>Advances replayable front/back VX price state and calculates compatible curve snapshots.</summary>
public static class FuturesVxTermStructureAccumulator
{
    /// <summary>Applies one immutable leg observation to an event-sourced checkpoint.</summary>
    public static FuturesVxTermStructureAccumulatorResult Apply(
        FuturesVxTermStructureSignalEntityId entityId,
        FuturesVxTermStructureCheckpoint? checkpoint,
        FuturesVxTermStructureLegObservation observation,
        FuturesVxTermStructureConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(configuration);
        checkpoint ??= new();
        Validate(entityId, observation, configuration);
        var previousLeg = observation.Leg == FuturesVxTermStructureLeg.Front
            ? checkpoint.Front : checkpoint.Back;
        if (previousLeg is not null
            && previousLeg.StreamEpochId == observation.StreamEpochId
            && observation.SourceSequence <= previousLeg.SourceSequence)
            throw new InvalidOperationException("A duplicate or stale VX leg observation cannot advance state.");

        var front = observation.Leg == FuturesVxTermStructureLeg.Front ? observation : checkpoint.Front;
        var back = observation.Leg == FuturesVxTermStructureLeg.Back ? observation : checkpoint.Back;
        var next = checkpoint with { Front = front, Back = back };
        if (front is null || back is null
            || front.StreamEpochId == Guid.Empty
            || front.StreamEpochId != back.StreamEpochId
            || (front.SourceTimestampUtc - back.SourceTimestampUtc).Duration() > configuration.MaximumSourceSkew)
            return new(next, null);

        var ratio = front.Price / back.Price;
        var percent = (back.Price / front.Price) - 1m;
        var signal = new FuturesVxTermStructureSignalReadModel
        {
            ValueDate = entityId.ValueDate,
            ConfigurationId = configuration.ConfigurationId,
            FrontVxContractId = front.ContractId,
            FrontExpiry = front.Expiry,
            FrontVxPrice = front.Price,
            BackVxContractId = back.ContractId,
            BackExpiry = back.Expiry,
            BackVxPrice = back.Price,
            FrontBackSpread = back.Price - front.Price,
            FrontBackRatio = ratio,
            TermStructurePercent = percent,
            TermStructureState = percent > configuration.FlatEpsilon
                ? FuturesVxTermStructureState.Contango
                : percent < -configuration.FlatEpsilon
                    ? FuturesVxTermStructureState.Backwardation
                    : FuturesVxTermStructureState.Flat,
            PriorFrontBackRatio = checkpoint.PreviousFrontBackRatio,
            PriorTermStructurePercent = checkpoint.PreviousTermStructurePercent,
            FrontSourceTimestampUtc = front.SourceTimestampUtc,
            BackSourceTimestampUtc = back.SourceTimestampUtc,
            FrontSourceSequence = front.SourceSequence,
            BackSourceSequence = back.SourceSequence,
            CalculatedAtUtc = front.SourceTimestampUtc >= back.SourceTimestampUtc
                ? front.SourceTimestampUtc : back.SourceTimestampUtc,
            IsWarm = true,
            IsValid = true
        };
        return new(next with
        {
            PreviousFrontBackRatio = ratio,
            PreviousTermStructurePercent = percent
        }, signal);
    }

    static void Validate(
        FuturesVxTermStructureSignalEntityId entityId,
        FuturesVxTermStructureLegObservation observation,
        FuturesVxTermStructureConfiguration configuration)
    {
        if (new FuturesVxTermStructureSignalEntityIdValidationRules().Execute(entityId).Length != 0)
            throw new ArgumentException("A valid VX term-structure entity ID is required.", nameof(entityId));
        if (observation.Leg is not (FuturesVxTermStructureLeg.Front or FuturesVxTermStructureLeg.Back))
            throw new ArgumentOutOfRangeException(nameof(observation));
        var expected = observation.Leg == FuturesVxTermStructureLeg.Front
            ? entityId.FrontContractId : entityId.BackContractId;
        if (!string.Equals(expected, observation.ContractId, StringComparison.Ordinal))
            throw new InvalidOperationException("The observed VX contract does not match its curve leg.");
        if (observation.Expiry == default || observation.Price <= 0
            || observation.SourceSequence < 0 || observation.SourceTimestampUtc.Offset != TimeSpan.Zero
            || observation.StreamEpochId == Guid.Empty)
            throw new InvalidOperationException("The VX leg observation is incomplete or invalid.");
        if (configuration.FlatEpsilon < 0 || configuration.MaximumSourceSkew < TimeSpan.Zero
            || string.IsNullOrWhiteSpace(configuration.ConfigurationId)
            || !string.Equals(configuration.ConfigurationId, entityId.ConfigurationId, StringComparison.Ordinal))
            throw new InvalidOperationException("The VX calculation configuration is invalid or incompatible.");
    }
}

/// <summary>Contains one accepted VX state transition and optional paired signal.</summary>
public sealed record FuturesVxTermStructureAccumulatorResult(
    FuturesVxTermStructureCheckpoint Checkpoint,
    FuturesVxTermStructureSignalReadModel? Signal);
