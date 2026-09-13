using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Command;

public static class AmendFuturesTradeEvidence
{
    /// <summary>Validates and applies an evidence amendment to an existing futures trade.</summary>
    /// <param name="command">The command containing the amendment identity and commission delta.</param>
    /// <param name="state">The event-sourced futures-trade state loaded for the command identity.</param>
    /// <returns>A successful result with a state-change event, or a rejected business rule.</returns>
    public static ServiceResult<GuidResult> Execute(
        this AmendFuturesTradeEvidenceCommand command,
        FuturesTradeCommandState state) => command switch
        {
            _ when state.Current is null =>
                command.UpdateFailed("TRADE.NOT_FOUND;Trade does not exist."),
            _ when command.AmendmentId == Guid.Empty =>
                command.UpdateFailed(
                    "TRADE.INVALID_AMENDMENT;Amendment ID is required."),
            _ => command.UpdatedOk(() => state.Update(
                command.CreateFuturesTradeChangedEvent(state.Current!),
                command))
        };

    /// <summary>Creates the private event containing the amended futures-trade evidence.</summary>
    /// <param name="command">The originating evidence-amendment command.</param>
    /// <param name="current">The current established futures-trade state.</param>
    /// <returns>The private futures-trade state-change event.</returns>
    static FuturesTradeChangedEvent CreateFuturesTradeChangedEvent(
        this AmendFuturesTradeEvidenceCommand command,
        EstablishedTradeDefinition current) => new()
        {
            EntityId = command.EntityId,
            State = current with
            {
                OpeningCommission = current.OpeningCommission + command.CommissionDelta,
                EvidenceRevision = checked(current.EvidenceRevision + 1),
                Status = EstablishedTradeStatus.Corrected
            },
            IsInitialEstablishment = false
        };
}
