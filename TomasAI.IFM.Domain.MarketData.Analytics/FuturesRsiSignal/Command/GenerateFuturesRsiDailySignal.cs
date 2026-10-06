using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class GenerateFuturesRsiDailySignal
{
    /// <summary>Computes and validates the Futures RSI Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesRsiDailySignalCommand command, FuturesRsiSignalCommandState state)
    {
        command.Compute(state.FuturesRsiSignals, out var futuresRsiSignalChange);
        if (!futuresRsiSignalChange.HasChanged)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply generated daily RSI signal events";
        var updated = futuresRsiSignalChange switch
        {
            _ when futuresRsiSignalChange.FuturesRsiSignal is null
                => command.UpdateFailed(ref errorMsg, "computed RSI signal is missing"),
            _ => state.Update(command.CreateFuturesRsiDailySignalEvents(futuresRsiSignalChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures RSI Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="previousFuturesRsiSignals">The previously accepted previous futures rsi signals used only as computation input.</param>
    /// <param name="futuresRsiSignalChange">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this GenerateFuturesRsiDailySignalCommand command,
        IReadOnlyCollection<FuturesRsiSignalReadModel> previousFuturesRsiSignals,
        out FuturesRsiSignalChange futuresRsiSignalChange)
    {
        var futuresRsiSignal = previousFuturesRsiSignals.GenerateRsiSignal(command.FuturesRsiSignalId, command.FuturesPrice);
        var acceptedHistory = previousFuturesRsiSignals.Append(futuresRsiSignal).TakeLast(256).ToArray();
        var outputWindow = command.EntityId.PeriodLength;
        var futuresRsiSignals = acceptedHistory.CanGenerateFuturesRsiSignals(outputWindow)
            ? acceptedHistory.GenerateFuturesRsiSignals(outputWindow) : null;
        futuresRsiSignalChange = new(futuresRsiSignal, null, futuresRsiSignals, true);
        return true;
    }

    /// <summary>Creates the Futures RSI Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresRsiSignalChange">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static IReadOnlyList<IEvent> CreateFuturesRsiDailySignalEvents(this GenerateFuturesRsiDailySignalCommand command,
        FuturesRsiSignalChange futuresRsiSignalChange)
    {
        var events = new List<IEvent>
        {
            command.CreateFuturesRsiDailySignalGeneratedEvent(futuresRsiSignalChange.FuturesRsiSignal!)
        };
        if (futuresRsiSignalChange.FuturesRsiSignals is { } futuresRsiSignals)
            events.Add(command.CreateFuturesRsiDailySignalsGeneratedEvent(
                futuresRsiSignalChange.FuturesRsiSignal!, futuresRsiSignals, command.EntityId.PeriodLength));
        return events;
    }


    /// <summary>Creates the Futures RSI Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresRsiSignal">The futures rsi signal business data used by this operation.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesRsiDailySignalGeneratedEvent CreateFuturesRsiDailySignalGeneratedEvent(this GenerateFuturesRsiDailySignalCommand command, FuturesRsiSignalReadModel futuresRsiSignal)
          => new()
          {
              CommandId = command.CommandId,
              Subject = new ActorSubject(ActorType.Event, FuturesRsiDailySignalGeneratedEvent.Actor, FuturesRsiDailySignalGeneratedEvent.Verb, command.EntityId.Format()),
              EntityId = command.EntityId,
              FuturesRsiSignal = futuresRsiSignal,
              CreatedBy = command.OriginatedBy,
              CreatedOn = command.OriginatedOn
          };

    /// <summary>Creates the Futures RSI Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresRsiSignal">The futures rsi signal business data used by this operation.</param>
    /// <param name="futuresRsiSignals">The ordered accepted signal history or computed publication window.</param>
    /// <param name="periodLength">The configured RSI period recorded with the published collection.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesRsiDailySignalsGeneratedEvent CreateFuturesRsiDailySignalsGeneratedEvent(
        this GenerateFuturesRsiDailySignalCommand command, FuturesRsiSignalReadModel futuresRsiSignal, IReadOnlyCollection<FuturesRsiSignalReadModel> futuresRsiSignals, int periodLength)
       => new()
       {
           CommandId = command.CommandId,
           Subject = new ActorSubject(ActorType.Event, FuturesRsiDailySignalsGeneratedEvent.Actor, FuturesRsiDailySignalsGeneratedEvent.Verb, command.EntityId.Format()),
           EntityId = command.EntityId,
           FuturesRsiSignalsId = new FuturesRsiSignalsId(futuresRsiSignal.ContractId, futuresRsiSignal.ValueDate, futuresRsiSignal.Timestamp),
           FuturesRsiSignals = [.. futuresRsiSignals],
           CreatedBy = command.OriginatedBy,
           CreatedOn = command.OriginatedOn
       };
}
