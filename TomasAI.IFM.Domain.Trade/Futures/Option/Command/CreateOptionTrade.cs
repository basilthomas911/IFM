using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Futures.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command;

public static class CreateOptionTrade
{
    /// <summary>
    /// Validates the current aggregate state and creates or idempotently reapplies an option trade.
    /// </summary>
    /// <param name="command">The command containing the established option-trade definition.</param>
    /// <param name="state">The event-sourced option-trade state loaded for the command identity.</param>
    /// <returns>
    /// A successful result after applying an <see cref="OptionTradeChangedEvent"/>, or a failed
    /// result describing the first rejected business rule.
    /// </returns>
    public static ServiceResult<GuidResult> Execute(
        this CreateOptionTradeCommand command,
        FuturesOptionTradeCommandState state)
    {
        var errorMsg = "EstablishedTrade.STATE.APPLY_FAILED: unable to apply CreateOptionTrade event";
        var updated = command.Compute(state, out var tradeChange) switch
        {
            _ when tradeChange.RejectionReason is not null => command.UpdateFailed(ref errorMsg, tradeChange.RejectionReason),
            _ when tradeChange.EstablishedTradeDefinition is null => command.UpdateFailed(ref errorMsg, "EstablishedTrade: the proposed trade definition is missing."),
            _ => state.Update(command.CreateOptionTradeChangedEvent(tradeChange), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed established trade definition without mutating actor state.</summary>
    /// Creates the private event that records the supplied established option-trade state.
    /// </summary>
    /// <param name="command">The originating create command.</param>
    /// <param name="trade">The established trade state to persist.</param>
    /// <param name="isInitialEstablishment">
    /// Indicates whether the event establishes the trade for the first time.
    /// </param>
    /// <returns>The immutable proposed established trade change.</returns>
    static EstablishedTradeChange CalculateTradeChange(
        this CreateOptionTradeCommand command,
        EstablishedTradeDefinition trade,
        bool isInitialEstablishment) => new(trade, isInitialEstablishment);
    /// <summary>Computes an established trade definition without changing state.</summary>
    /// <param name="command">The concrete trade lifecycle intent.</param>
    /// <param name="state">The current established trade owner.</param>
    /// <param name="tradeChange">The proposed trade definition or business rejection.</param>
    /// <returns>True when the proposed trade change is accepted.</returns>
    internal static bool Compute(this CreateOptionTradeCommand command, FuturesOptionTradeCommandState state, out EstablishedTradeChange tradeChange)
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
            _ when command.Trade.AssetFamily != TradeAssetFamily.FuturesOption =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.TYPE_MISMATCH;OptionTrade requires FuturesOption asset family."),
            _ when command.Trade.StrategyKind == TradeStrategyKind.FuturesOutright =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.TYPE_MISMATCH;OptionTrade cannot use FuturesOutright strategy."),
            _ => command.CalculateTradeChange(command.Trade, true)
        };
        return tradeChange.RejectionReason is null;
    }
    /// <summary>Creates the source event with the originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="tradeChange">The accepted immutable trade change.</param>
    /// <returns>The event ready for state application.</returns>
    internal static OptionTradeChangedEvent CreateOptionTradeChangedEvent(this CreateOptionTradeCommand command, EstablishedTradeChange tradeChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, "FuturesOptionTradeEvent", OptionTradeChangedEvent.Verb, command.EntityId.Format()),
            ReceivedOn = DateTime.UtcNow,
            EntityId = command.EntityId,
            EstablishedTradeDefinition = tradeChange.EstablishedTradeDefinition!,
            IsInitialEstablishment = tradeChange.IsInitialEstablishment
        };
}
