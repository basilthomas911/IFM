using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

/// <summary>Starts ADX after computing and validating its restoration or historical initialization.</summary>
public static class StartFuturesAdxSignal
{
    /// <summary>Computes and validates the Futures ADX Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this StartFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
    {
        var errorMsg = "unable to apply ADX start event";
        var updated = command.Compute(state.AdxSignals, out var futuresAdxInitialization) switch
        {
            _ when !futuresAdxInitialization.Accepted
                => command.UpdateFailed(ref errorMsg, futuresAdxInitialization.RejectionReason!),
            _ => state.Update(command.CreateFuturesAdxSignalStartedEvent(futuresAdxInitialization),
                futuresAdxInitialization.HistoricalSignals, command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures ADX Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="adxSignals">The ordered accepted signal history or computed publication window.</param>
    /// <param name="futuresAdxInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this StartFuturesAdxSignalCommand command,
        IReadOnlyCollection<FuturesAdxSignalReadModel> adxSignals, out FuturesAdxInitialization futuresAdxInitialization)
    {
        var seededSignals = new List<FuturesAdxSignalReadModel>();
        var historicalEvents = new List<FuturesAdxSignalGeneratedEvent>();
        var hasHistoricalSeed = command.HistoricalSeed is { Length: > 0 };
        string? rejectionReason = command.EntityId.PeriodLength <= 0 ? "ADX period length must be positive" : null;
        if (rejectionReason is null && hasHistoricalSeed)
        {
            foreach (var bar in command.HistoricalSeed!)
            {
                var id = new FuturesAdxSignalId(command.EntityId.ContractId, command.EntityId.ValueDate,
                    command.EntityId.TimePeriod, command.EntityId.PeriodLength,
                    TimeOnly.FromDateTime(bar.LastMarketEventUtc.UtcDateTime));
                var generated = new GenerateFuturesAdxSignalCommand(id, bar.Close, bar, true)
                {
                    CommandId = command.CommandId,
                    Subject = new ActorSubject(ActorType.Command, GenerateFuturesAdxSignalCommand.Actor,
                        GenerateFuturesAdxSignalCommand.Verb, command.EntityId.Format())
                };
                if (!generated.Compute(seededSignals.LastOrDefault(), seededSignals, out var futuresAdxSignalCompute)
                    || !futuresAdxSignalCompute.IsValid)
                {
                    rejectionReason = "ADX historical initialization produced invalid directional values";
                    break;
                }
                var historicalEvent = generated.CreateFuturesAdxSignalGeneratedEvent(
                    futuresAdxSignalCompute.SignalDirection, futuresAdxSignalCompute);
                historicalEvents.Add(historicalEvent);
                seededSignals.Add(historicalEvent.FuturesAdxSignal);
            }
            if (rejectionReason is null && seededSignals.LastOrDefault()?.IsWarm != true)
                rejectionReason = "ADX historical initialization did not reach a warm state";
        }
        futuresAdxInitialization = new(hasHistoricalSeed ? null : adxSignals.LastOrDefault(),
            hasHistoricalSeed, historicalEvents, rejectionReason);
        return futuresAdxInitialization.Accepted;
    }

    /// <summary>Creates the Futures ADX Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresAdxInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesAdxSignalStartedEvent CreateFuturesAdxSignalStartedEvent(
        this StartFuturesAdxSignalCommand command, FuturesAdxInitialization futuresAdxInitialization) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesAdxSignalStartedEvent.Actor, FuturesAdxSignalStartedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        StartedOn = command.OriginatedOn,
        StartedBy = command.OriginatedBy,
        RestoredSignal = futuresAdxInitialization.RestoredSignal,
        ResetForHistoricalSeed = futuresAdxInitialization.ResetForHistoricalSeed
    };
}
