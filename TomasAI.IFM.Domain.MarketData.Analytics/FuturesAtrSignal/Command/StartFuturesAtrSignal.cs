using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

/// <summary>Handles one start transition for the futures ATR signal.</summary>
public static class StartFuturesAtrSignal
{
    /// <summary>Computes and validates the Futures ATR Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this StartFuturesAtrSignalCommand command, FuturesAtrSignalCommandState state)
    {
        var errorMsg = "unable to apply ATR start event";
        var updated = command.Compute(state.AtrSignal, out var futuresAtrInitialization) switch
        {
            _ when !futuresAtrInitialization.Accepted
                => command.UpdateFailed(ref errorMsg, futuresAtrInitialization.RejectionReason!),
            _ => state.Update(command.CreateFuturesAtrSignalStartedEvent(futuresAtrInitialization),
                futuresAtrInitialization.HistoricalSignals, command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures ATR Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="currentFuturesAtrSignal">The previously accepted current futures atr signal used only as computation input.</param>
    /// <param name="futuresAtrInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this StartFuturesAtrSignalCommand command,
        FuturesAtrSignalReadModel? currentFuturesAtrSignal, out FuturesAtrInitialization futuresAtrInitialization)
    {
        var seededSignals = new List<FuturesAtrSignalReadModel>();
        var historicalEvents = new List<FuturesAtrSignalGeneratedEvent>();
        var hasHistoricalSeed = command.HistoricalSeed is { Length: > 0 };
        string? rejectionReason = null;
        FuturesAtrAccumulatorCheckpoint? futuresAtrCheckpoint = null;
        if (hasHistoricalSeed)
        {
            foreach (var bar in command.HistoricalSeed!)
            {
                var id = new FuturesAtrSignalId(command.EntityId.ContractId, command.EntityId.ValueDate,
                    command.EntityId.TimePeriod, command.EntityId.PeriodLength,
                    TimeOnly.FromDateTime(bar.LastMarketEventUtc.UtcDateTime));
                var generated = new GenerateFuturesAtrSignalCommand(id, bar.Close, bar, true)
                {
                    CommandId = command.CommandId,
                    Subject = new(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor,
                        GenerateFuturesAtrSignalCommand.Verb, command.EntityId.Format())
                };
                generated.Compute(futuresAtrCheckpoint, out var futuresAtrSignalChange);
                if (!futuresAtrSignalChange.Accepted)
                {
                    rejectionReason = futuresAtrSignalChange.RejectionReason;
                    break;
                }
                if (!futuresAtrSignalChange.HasChanged) continue;
                var historicalEvent = generated.CreateFuturesAtrSignalGeneratedEvent(futuresAtrSignalChange);
                futuresAtrCheckpoint = futuresAtrSignalChange.FuturesAtrCheckpoint;
                historicalEvents.Add(historicalEvent);
                seededSignals.Add(historicalEvent.FuturesAtrSignal);
            }
            if (rejectionReason is null && (seededSignals.LastOrDefault() is not { IsWarm: true, AtrRatio: not null }))
                rejectionReason = "ATR historical initialization did not reach a warm state";
        }
        futuresAtrInitialization = new(hasHistoricalSeed ? null : currentFuturesAtrSignal,
            hasHistoricalSeed, historicalEvents, rejectionReason);
        return futuresAtrInitialization.Accepted;
    }

    /// <summary>Creates the Futures ATR Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresAtrInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesAtrSignalStartedEvent CreateFuturesAtrSignalStartedEvent(
        this StartFuturesAtrSignalCommand command, FuturesAtrInitialization futuresAtrInitialization) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesAtrSignalStartedEvent.Actor, FuturesAtrSignalStartedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        StartedOn = command.OriginatedOn,
        StartedBy = command.OriginatedBy,
        RestoredSignal = futuresAtrInitialization.RestoredSignal,
        ResetForHistoricalSeed = futuresAtrInitialization.ResetForHistoricalSeed
    };
}
