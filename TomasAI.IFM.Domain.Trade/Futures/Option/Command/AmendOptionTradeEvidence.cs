using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Command;

public static class AmendOptionTradeEvidence
{
    /// <summary>
    /// Validates and applies an evidence amendment to an existing option trade.
    /// </summary>
    /// <param name="command">The command containing the amendment identity and commission delta.</param>
    /// <param name="state">The event-sourced option-trade state loaded for the command identity.</param>
    /// <returns>
    /// A successful result after applying an <see cref="OptionTradeChangedEvent"/>, or a failed
    /// result when the trade or amendment identity is unavailable.
    /// </returns>
    public static ServiceResult<GuidResult> Execute(
        this AmendOptionTradeEvidenceCommand command,
        FuturesOptionTradeCommandState state) => command switch
        {
            _ when state.Current is null =>
                command.UpdateFailed("TRADE.NOT_FOUND;Trade does not exist."),
            _ when command.AmendmentId == Guid.Empty =>
                command.UpdateFailed(
                    "TRADE.INVALID_AMENDMENT;Amendment ID is required."),
            _ => command.UpdatedOk(() => state.Update(
                command.CreateOptionTradeChangedEvent(state.Current!),
                command))
        };

    /// <summary>
    /// Creates the private event containing the amended commission, evidence revision, and status.
    /// </summary>
    /// <param name="command">The originating evidence-amendment command.</param>
    /// <param name="current">The current established option-trade state.</param>
    /// <returns>The private option-trade state-change event.</returns>
    static OptionTradeChangedEvent CreateOptionTradeChangedEvent(
        this AmendOptionTradeEvidenceCommand command,
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
