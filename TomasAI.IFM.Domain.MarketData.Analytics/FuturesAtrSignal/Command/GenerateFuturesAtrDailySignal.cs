using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

/// <summary>Handles cross-value-date Daily, Weekly, and Monthly Futures ATR command streams.</summary>
public static class GenerateFuturesAtrDailySignal
{
    /// <summary>Computes and validates the Futures ATR Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesAtrDailySignalCommand command, FuturesAtrSignalCommandState state)
    {
        command.Compute(state.FuturesAtrCheckpoint, out var futuresAtrSignalChange);
        if (futuresAtrSignalChange.Accepted && !futuresAtrSignalChange.HasChanged)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply generated Wilder ATR event";
        var updated = futuresAtrSignalChange switch
        {
            _ when !futuresAtrSignalChange.Accepted
                => command.UpdateFailed(ref errorMsg, futuresAtrSignalChange.RejectionReason!),
            _ when futuresAtrSignalChange.FuturesAtrCheckpoint is null || futuresAtrSignalChange.FuturesAtrSignal is null
                => command.UpdateFailed(ref errorMsg, "computed Wilder ATR checkpoint or signal is missing"),
            _ => state.Update(command.CreateFuturesAtrDailySignalGeneratedEvent(futuresAtrSignalChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures ATR Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresAtrCheckpoint">The immutable accumulator checkpoint to read or record for this domain transition.</param>
    /// <param name="futuresAtrSignalChange">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this GenerateFuturesAtrDailySignalCommand command, FuturesAtrAccumulatorCheckpoint? futuresAtrCheckpoint,
        out FuturesAtrSignalChange futuresAtrSignalChange)
    {
        var observation = command.Observation;
        var rejectionReason = observation switch
        {
            null => "a completed trade-session bar observation is required",
            _ when !observation.IsComplete || !observation.IsValid => "source bar must be valid and completed",
            _ when command.EntityId.PeriodLength <= 0 => "ATR period length must be positive",
            _ when !FuturesAtrDailySignalActivationProfile.IsSupported(command.EntityId.TimePeriod)
                || observation.TimeFrame != TimeFrameType.Daily
                || !string.Equals(observation.ContractId, command.EntityId.ContractId, StringComparison.Ordinal)
                || observation.ValueDate != command.FuturesAtrSignalId.ValueDate => "The daily observation does not match the day-based ATR identity.",
            _ => null
        };
        if (rejectionReason is not null)
        {
            futuresAtrSignalChange = new(null, futuresAtrCheckpoint, false, rejectionReason);
            return false;
        }
        var hasChanged = FuturesAtrWilderAccumulator.TryApply(observation!, command.EntityId.PeriodLength,
            futuresAtrCheckpoint, out var futuresAtrWilderResult);
        futuresAtrSignalChange = hasChanged
            ? new(FuturesAtrWilderSignalFactory.Create(command.FuturesAtrSignalId, observation!, futuresAtrWilderResult),
                futuresAtrWilderResult.Checkpoint, true, null)
            : new(null, futuresAtrCheckpoint, false, null);
        return hasChanged;
    }

    /// <summary>Creates the Futures ATR Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresAtrSignalChange">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesAtrDailySignalGeneratedEvent CreateFuturesAtrDailySignalGeneratedEvent(this GenerateFuturesAtrDailySignalCommand command,
        FuturesAtrSignalChange futuresAtrSignalChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesAtrDailySignalGeneratedEvent.Actor, FuturesAtrDailySignalGeneratedEvent.Verb, command.FuturesAtrSignalId.ToDailyEntityId().Format()),
        EntityId = command.FuturesAtrSignalId.ToDailyEntityId(),
        FuturesAtrSignal = futuresAtrSignalChange.FuturesAtrSignal!,
        FuturesAtrCheckpoint = futuresAtrSignalChange.FuturesAtrCheckpoint!,
        CreatedBy = command.OriginatedBy,
        CreatedOn = command.OriginatedOn
    };
}
