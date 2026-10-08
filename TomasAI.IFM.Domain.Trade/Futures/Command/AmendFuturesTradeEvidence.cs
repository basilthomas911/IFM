using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Futures.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Futures.Command;

public static class AmendFuturesTradeEvidence
{
    /// <summary>Validates and applies an evidence amendment to an existing futures trade.</summary>
    /// <param name="command">The command containing the amendment identity and commission delta.</param>
    /// <param name="state">The event-sourced futures-trade state loaded for the command identity.</param>
    /// <returns>A successful result with a state-change event, or a rejected business rule.</returns>
    public static ServiceResult<GuidResult> Execute(
        this AmendFuturesTradeEvidenceCommand command,
        FuturesTradeCommandState state)
    {
        var errorMsg = "EstablishedTrade.STATE.APPLY_FAILED: unable to apply AmendFuturesTradeEvidence event";
        var updated = command.Compute(state, out var tradeChange) switch
        {
            _ when tradeChange.RejectionReason is not null => command.UpdateFailed(ref errorMsg, tradeChange.RejectionReason),
            _ when tradeChange.EstablishedTradeDefinition is null => command.UpdateFailed(ref errorMsg, "EstablishedTrade: the proposed trade definition is missing."),
            _ => state.Update(command.CreateFuturesTradeChangedEvent(tradeChange), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed established trade definition without mutating actor state.</summary>
    /// <param name="command">The originating evidence-amendment command.</param>
    /// <param name="current">The current established futures-trade state.</param>
    /// <returns>The immutable proposed established trade change.</returns>
    static EstablishedTradeChange CalculateTradeChange(
        this AmendFuturesTradeEvidenceCommand command,
        EstablishedTradeDefinition current) => new(current with
            {
                OpeningCommission = current.OpeningCommission + command.CommissionDelta,
                EvidenceRevision = checked(current.EvidenceRevision + 1),
                Status = EstablishedTradeStatus.Corrected
            }, false);
    /// <summary>Computes an established trade definition without changing state.</summary>
    /// <param name="command">The concrete trade lifecycle intent.</param>
    /// <param name="state">The current established trade owner.</param>
    /// <param name="tradeChange">The proposed trade definition or business rejection.</param>
    /// <returns>True when the proposed trade change is accepted.</returns>
    internal static bool Compute(this AmendFuturesTradeEvidenceCommand command, FuturesTradeCommandState state, out EstablishedTradeChange tradeChange)
    {
        tradeChange = command switch
        {
            _ when state.EstablishedTradeDefinition is null =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.NOT_FOUND;Trade does not exist."),
            _ when command.AmendmentId == Guid.Empty =>
                new EstablishedTradeChange(null, RejectionReason: "TRADE.INVALID_AMENDMENT;Amendment ID is required."),
            _ => command.CalculateTradeChange(state.EstablishedTradeDefinition!)
        };
        return tradeChange.RejectionReason is null;
    }
    /// <summary>Creates the source event with the originating command identity.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="tradeChange">The accepted immutable trade change.</param>
    /// <returns>The event ready for state application.</returns>
    internal static FuturesTradeChangedEvent CreateFuturesTradeChangedEvent(this AmendFuturesTradeEvidenceCommand command, EstablishedTradeChange tradeChange)
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
