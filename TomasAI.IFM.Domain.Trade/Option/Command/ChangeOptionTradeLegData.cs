using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

public static class ChangeOptionTradeLegData
{
    /// <summary>
    /// Executes the command to change option trade leg data, validating trade existence and status before applying
    /// changes.
    /// </summary>
    /// <param name="e">The command containing the option trade leg data changes to apply.</param>
    /// <param name="state">The current state of the option trade command.</param>
    /// <returns>true if the option leg data was changed and applied; otherwise, false.</returns>
    /// <exception cref="ChangeOptionTradeLegDataException">Thrown when the trade does not exist or the trade status is not in INTRADAY status.</exception>
    public static bool Execute(this ChangeOptionTradeLegDataCommand e, OptionTradeCommandState state)
    {
        return e switch
        {
            _ when !state.TradeExists(e.EntityId) => throw new ChangeOptionTradeLegDataException(
                $"{e.CommandName}: trade: {e.TradeId} orderId: {e.OrderId} does not exist"),
            _ when !state.IsTradeInIntraDayStatus(e.TradeStatus) => throw new ChangeOptionTradeLegDataException(
                $"{e.CommandName}: trade status = {e.TradeStatus} must be in INTRADAY status"),
            _ => ChangeOptionTradeLegData(e, state)
        };

        /// <summary>
        /// Updates the option trade leg data and trade positions based on the specified command and state.
        /// </summary>
        /// <param name="e">The command containing the updated option trade leg data.</param>
        /// <param name="state">The current state of the option trade.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        static bool ChangeOptionTradeLegData(ChangeOptionTradeLegDataCommand e, OptionTradeCommandState state)
        {
            var tradePositionId = new TradePositionEntityId(
                           OrderId: e.OrderId,
                           TradeId: e.TradeId,
                           TradeType: e.TradeType,
                           ValueDate: e.ValueDate,
                           DaysToExpiry: state.DaysToExpiry(e.ValueDate),
                           TradeStatus: e.TradeStatus);

            if (!state.TradePositionExists(tradePositionId))
            {
                state.Update(state.TradePositions
                            .GetAddedEvent(tradePositionId, state.TradeType, e.AssetPrice, e.RiskFreeRate, e.OriginatedOn, e.OriginatedBy), e);
            }

            if (state.HasOptionLegDataChanged(e))
            {
                state.Update(e.CreateOptionTradeLegDataChangedEvent(tradePositionId), e);

                return state
                    .Update(state.TradePositions
                        .GetUpdatedEvent(tradePositionId, state.TradeType, e.OptionLegData.OptionLegId, e.OriginatedOn, e.OriginatedBy), e);
            }
            return false;
        }
    }

    /// <summary>
    /// Creates an OptionTradeLegDataChangedEvent from the command and trade position identifier.
    /// </summary>
    /// <param name="e">The command containing the option trade leg data changes.</param>
    /// <param name="tradePositionId">The identifier of the associated trade position.</param>
    /// <returns>A new OptionTradeLegDataChangedEvent initialized with data from the command and trade position identifier.</returns>
    internal static OptionTradeLegDataChangedEvent CreateOptionTradeLegDataChangedEvent(this ChangeOptionTradeLegDataCommand e, TradePositionEntityId tradePositionId)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeLegDataChangedEvent.Actor, OptionTradeLegDataChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.OrderId,
            Key = tradePositionId,
            OptionLegData = e.OptionLegData.Copy(),
            AssetPrice = e.AssetPrice,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

}
