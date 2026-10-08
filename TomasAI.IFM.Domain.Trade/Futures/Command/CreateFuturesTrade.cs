using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Futures.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Futures.Command;

public static class CreateFuturesTrade
{
    /// <summary>
    /// Validates the current aggregate state and creates or idempotently reapplies a futures trade.
    /// </summary>
    /// <param name="command">The command containing the established futures-trade definition.</param>
    /// <param name="state">The event-sourced futures-trade state loaded for the command identity.</param>
    /// <returns>A successful result with a state-change event, or the first rejected business rule.</returns>
    public static ServiceResult<GuidResult> Execute(
        this CreateFuturesTradeCommand command,
        FuturesTradeCommandState state)
    {
        var errorMsg = "EstablishedTrade.STATE.APPLY_FAILED: unable to apply CreateFuturesTrade event";
        var updated = command.Compute(state, out var tradeChange) switch
        {
            _ when tradeChange.RejectionReason is not null => command.UpdateFailed(ref errorMsg, tradeChange.RejectionReason),
            _ when tradeChange.EstablishedTradeDefinition is null => command.UpdateFailed(ref errorMsg, "EstablishedTrade: the proposed trade definition is missing."),
            _ => state.Update(command.CreateFuturesTradeChangedEvent(tradeChange), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed established trade definition without mutating actor state.</summary>
    /// <param name="command">The originating create command.</param>
    /// <param name="trade">The established futures-trade state.</param>
    /// <param name="isInitialEstablishment">Whether the event first establishes the trade.</param>
    /// <returns>The immutable proposed established trade change.</returns>
    static EstablishedTradeChange CalculateTradeChange(
        this CreateFuturesTradeCommand command,
        EstablishedTradeDefinition trade,
        bool isInitialEstablishment) => new(trade, isInitialEstablishment);
    /// <summary>Computes an established trade definition without changing state.</summary>
    /// <param name="command">The concrete trade lifecycle intent.</param>
    /// <param name="state">The current established trade owner.</param>
    /// <param name="tradeChange">The proposed trade definition or business rejection.</param>
    /// <returns>True when the proposed trade change is accepted.</returns>
    internal static bool Compute(this CreateFuturesTradeCommand command, FuturesTradeCommandState state, out EstablishedTradeChange tradeChange)
    {
        tradeChange = command switch
        {
            _ when state.EstablishedTradeDefinition is { } current &&
                   current.Id == command.Trade.Id &&
                   current.ExecutionAttemptId == command.Trade.ExecutionAttemptId =>
                command.CalculateTradeChange(current, false),
            _ when state.EstablishedTradeDefinition is not null =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.ALREADY_EXISTS;A different established Trade already owns this identity."),
            _ when !command.Trade.Id.IsValid ||
                   command.Trade.SourceComponentId == Guid.Empty ||
                   command.Trade.ExecutionAttemptId == Guid.Empty ||
                   command.Trade.Legs.Length == 0 ||
                   command.Trade.OriginalFills.Length == 0 ||
                   command.Trade.EstablishedAtUtc.Kind != DateTimeKind.Utc =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.INVALID;Trade identity, component, execution, UTC time, Legs, and OriginalFills are required."),
            _ when command.Trade.AssetFamily != TradeAssetFamily.Futures =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.TYPE_MISMATCH;Futures Trade requires Futures asset family."),
            _ when command.Trade.StrategyKind != TradeStrategyKind.FuturesOutright =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.TYPE_MISMATCH;Futures Trade requires FuturesOutright strategy."),
            _ => command.CalculateTradeChange(command.Trade, true)
        };
        return tradeChange.RejectionReason is null;
    }
    /// <summary>Creates the source event with the originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="tradeChange">The accepted immutable trade change.</param>
    /// <returns>The event ready for state application.</returns>
    internal static FuturesTradeChangedEvent CreateFuturesTradeChangedEvent(this CreateFuturesTradeCommand command, EstablishedTradeChange tradeChange)
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
