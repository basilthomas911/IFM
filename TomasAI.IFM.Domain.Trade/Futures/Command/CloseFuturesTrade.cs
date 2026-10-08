using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Futures.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Futures.Command;

public static class CloseFuturesTrade
{
    /// <summary>Validates the current futures-trade status and completes its close transition.</summary>
    /// <param name="command">The command requesting final closure of the futures trade.</param>
    /// <param name="state">The event-sourced futures-trade state loaded for the command identity.</param>
    /// <returns>A successful result with a state-change event, or a rejected business rule.</returns>
    public static ServiceResult<GuidResult> Execute(
        this CloseFuturesTradeCommand command,
        FuturesTradeCommandState state)
    {
        var errorMsg = "EstablishedTrade.STATE.APPLY_FAILED: unable to apply CloseFuturesTrade event";
        var updated = command.Compute(state, out var tradeChange) switch
        {
            _ when tradeChange.RejectionReason is not null => command.UpdateFailed(ref errorMsg, tradeChange.RejectionReason),
            _ when tradeChange.EstablishedTradeDefinition is null => command.UpdateFailed(ref errorMsg, "EstablishedTrade: the proposed trade definition is missing."),
            _ => state.Update(command.CreateFuturesTradeChangedEvent(tradeChange), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed established trade definition without mutating actor state.</summary>
    /// <param name="command">The originating close command.</param>
    /// <param name="current">The current established futures-trade state.</param>
    /// <returns>The immutable proposed established trade change.</returns>
    static EstablishedTradeChange CalculateTradeChange(
        this CloseFuturesTradeCommand command,
        EstablishedTradeDefinition current) => new(ApplyClosingEvidence(current, command), false);

    /// <summary>Evaluates apply closing evidence business information.</summary>
    /// <param name="current">The current business snapshot.</param>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static EstablishedTradeDefinition ApplyClosingEvidence(EstablishedTradeDefinition current, CloseFuturesTradeCommand command)
    {
        if (!TradeCloseEvidence.TryApply(current, command.ClosingFills, command.ClosedAtUtc, out var updated))
            throw new InvalidOperationException("Validated closing evidence changed.");
        return updated;
    }
    /// <summary>Computes an established trade definition without changing state.</summary>
    /// <param name="command">The concrete trade lifecycle intent.</param>
    /// <param name="state">The current established trade owner.</param>
    /// <param name="tradeChange">The proposed trade definition or business rejection.</param>
    /// <returns>True when the proposed trade change is accepted.</returns>
    internal static bool Compute(this CloseFuturesTradeCommand command, FuturesTradeCommandState state, out EstablishedTradeChange tradeChange)
    {
        tradeChange = command switch
        {
            _ when state.EstablishedTradeDefinition is null =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.NOT_FOUND;Trade does not exist."),
            _ when state.EstablishedTradeDefinition.Status != EstablishedTradeStatus.Closing =>
                new EstablishedTradeChange(null, RejectionReason: $"TRADE.INVALID_TRANSITION;Cannot close from {state.EstablishedTradeDefinition.Status}."),
            _ when !TradeCloseEvidence.TryApply(state.EstablishedTradeDefinition, command.ClosingFills, command.ClosedAtUtc, out _) =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.INVALID_CLOSE_EVIDENCE;Closing fills and a UTC close time are required."),
            _ => command.CalculateTradeChange(state.EstablishedTradeDefinition!)
        };
        return tradeChange.RejectionReason is null;
    }
    /// <summary>Creates the source event with the originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="tradeChange">The accepted immutable trade change.</param>
    /// <returns>The event ready for state application.</returns>
    internal static FuturesTradeChangedEvent CreateFuturesTradeChangedEvent(this CloseFuturesTradeCommand command, EstablishedTradeChange tradeChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, "FuturesTradeEvent", FuturesTradeChangedEvent.Verb, command.EntityId.Format()),
            ReceivedOn = DateTime.UtcNow,
            EntityId = command.EntityId,
            EstablishedTradeDefinition = tradeChange.EstablishedTradeDefinition!,
            IsInitialEstablishment = tradeChange.IsInitialEstablishment
        };
}
