using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

/// <summary>Handles one start transition for the futures MACD signal.</summary>
public static class StartFuturesMacdSignal
{
    /// <summary>Computes and validates the Futures MACD Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this StartFuturesMacdSignalCommand command, FuturesMacdSignalCommandState state)
    {
        var errorMsg = "unable to apply MACD start event";
        var updated = command.Compute(state.MacdSignals.LastOrDefault(), out var futuresMacdInitialization) switch
        {
            _ when !futuresMacdInitialization.Accepted
                => command.UpdateFailed(ref errorMsg, futuresMacdInitialization.RejectionReason!),
            _ => state.Update(command.CreateFuturesMacdSignalStartedEvent(futuresMacdInitialization),
                futuresMacdInitialization.HistoricalSignals, command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures MACD Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="currentFuturesMacdSignal">The previously accepted current futures macd signal used only as computation input.</param>
    /// <param name="futuresMacdInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this StartFuturesMacdSignalCommand command,
        FuturesMacdSignalReadModel? currentFuturesMacdSignal, out FuturesMacdInitialization futuresMacdInitialization)
    {
        var seededSignals = new List<FuturesMacdSignalReadModel>();
        var historicalEvents = new List<FuturesMacdSignalGeneratedEvent>();
        var hasHistoricalSeed = command.HistoricalSeed is { Length: > 0 };
        string? rejectionReason = null;
        if (hasHistoricalSeed)
        {
            foreach (var bar in command.HistoricalSeed!)
            {
                var id = new FuturesMacdSignalId(command.EntityId.ContractId, command.EntityId.ValueDate,
                    command.EntityId.TimePeriod, command.EntityId.SignalEmaPeriod, command.EntityId.FastEmaPeriod, command.EntityId.SlowEmaPeriod,
                    TimeOnly.FromDateTime(bar.LastMarketEventUtc.UtcDateTime));
                var generated = new GenerateFuturesMacdSignalCommand(id, bar.Close, bar, true)
                {
                    CommandId = command.CommandId,
                    Subject = new(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor,
                        GenerateFuturesMacdSignalCommand.Verb, command.EntityId.Format())
                };
                generated.Compute(seededSignals, out var futuresMacdSignalCompute);
                if (!futuresMacdSignalCompute.IsValid)
                {
                    rejectionReason = "MACD historical initialization produced invalid accumulator values";
                    break;
                }
                var historicalEvent = generated.CreateFuturesMacdSignalGeneratedEvent(
                    futuresMacdSignalCompute.SignalDirection, futuresMacdSignalCompute);
                historicalEvents.Add(historicalEvent);
                seededSignals.Add(historicalEvent.FuturesMacdSignal);
            }
            if (rejectionReason is null && (seededSignals.LastOrDefault()?.IsWarm != true))
                rejectionReason = "MACD historical initialization did not reach a warm state";
        }
        futuresMacdInitialization = new(hasHistoricalSeed ? null : currentFuturesMacdSignal,
            hasHistoricalSeed, historicalEvents, rejectionReason);
        return futuresMacdInitialization.Accepted;
    }

    /// <summary>Creates the Futures MACD Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresMacdInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesMacdSignalStartedEvent CreateFuturesMacdSignalStartedEvent(
        this StartFuturesMacdSignalCommand command, FuturesMacdInitialization futuresMacdInitialization) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesMacdSignalStartedEvent.Actor, FuturesMacdSignalStartedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        StartedOn = command.OriginatedOn,
        StartedBy = command.OriginatedBy,
        RestoredSignal = futuresMacdInitialization.RestoredSignal,
        ResetForHistoricalSeed = futuresMacdInitialization.ResetForHistoricalSeed
    };
}
