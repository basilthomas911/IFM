using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

internal static class UpdateOptionTradeSpreadDistributionStatistics
{
    /// <summary>
    /// Executes the command to update option trade spread distribution statistics with validation.
    /// </summary>
    /// <param name="e">The command containing the distribution statistics to update.</param>
    /// <param name="state">The current state of the option trade command.</param>
    /// <returns>True if the statistics were successfully updated; otherwise, false.</returns>
    /// <exception cref="ChangeOptionTradeDistributionStatisticsException">Thrown when the trade does not exist or the trade status is not in INTRADAY status.</exception>
    public static bool Execute(this UpdateOptionTradeSpreadDistributionStatisticsCommand e, OptionTradeCommandState state)
    {
        return e switch
        {
            _ when !state.TradeExists(e.EntityId) => throw new ChangeOptionTradeDistributionStatisticsException(
                $"{e.CommandName}: trade: {e.TradeId} orderId: {e.OrderId} does not exist"),
            _ when !state.IsTradeInIntraDayStatus(e.TradeStatus) => throw new ChangeOptionTradeDistributionStatisticsException(
                $"{e.CommandName}: trade status = {e.TradeStatus} must be in INTRADAY status"),
            _ => ChangeOptionTradeDistributionStatistics(e, state)
        };

        /// <summary>
        /// Updates the option trade distribution statistics and trade positions based on the specified command and state.
        /// </summary>
        /// <param name="e">The command containing the updated distribution statistics.</param>
        /// <param name="state">The current state of the option trade.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        static bool ChangeOptionTradeDistributionStatistics(UpdateOptionTradeSpreadDistributionStatisticsCommand e, OptionTradeCommandState state)
        {
            state.Update(e.CreateOptionTradeSpreadDistributionStatisticsUpdatedEvent(), e);
            return state.Update(e.CreateTradePositionUpdatedEvent(
                state.GetIntraDayTradePosition(state.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread, e.ValueDate)!,
                state.GetIntraDayTradePosition(state.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread, e.ValueDate)!), e);
        }

    }

    /// <summary>
    /// Creates an <see cref="OptionTradeSpreadDistributionStatisticsUpdatedEvent"/> from an
    /// <see cref="UpdateOptionTradeSpreadDistributionStatisticsCommand"/>.
    /// Captures both put and call spread distributions along with trade status and value date.
    /// </summary>
    /// <param name="e">The command containing the updated spread distribution statistics.</param>
    /// <returns>A fully populated <see cref="OptionTradeSpreadDistributionStatisticsUpdatedEvent"/>.</returns>
    internal static OptionTradeSpreadDistributionStatisticsUpdatedEvent CreateOptionTradeSpreadDistributionStatisticsUpdatedEvent(this UpdateOptionTradeSpreadDistributionStatisticsCommand e)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeSpreadDistributionStatisticsUpdatedEvent.Actor, OptionTradeSpreadDistributionStatisticsUpdatedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OrderId = e.OrderId,
            TradeId = e.TradeId,
            TradeStatus = e.TradeStatus,
            ValueDate = e.ValueDate,
            PutSpreadDistribution = e.PutSpreadDistribution,
            CallSpreadDistribution = e.CallSpreadDistribution,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };


    /// <summary>
    /// Creates a <see cref="TradePositionUpdatedEvent"/> from an
    /// <see cref="UpdateOptionTradeSpreadDistributionStatisticsCommand"/> after spread distribution statistics
    /// have been recalculated. Sets the change source to <see cref="TradePositionChangeSourceType.SpreadDistributionStatistics"/>
    /// and attaches the updated put and call trade position read models.
    /// </summary>
    /// <param name="e">The command that triggered the spread distribution statistics update.</param>
    /// <param name="putTradePosition">The updated read model for the put spread trade position.</param>
    /// <param name="callTradePosition">The updated read model for the call spread trade position.</param>
    /// <returns>A fully populated <see cref="TradePositionUpdatedEvent"/>.</returns>
    internal static TradePositionUpdatedEvent CreateTradePositionUpdatedEvent(this UpdateOptionTradeSpreadDistributionStatisticsCommand e,
        TradePositionReadModel putTradePosition, TradePositionReadModel callTradePosition)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, TradePositionUpdatedEvent.Actor, TradePositionUpdatedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            PutTradePosition = putTradePosition,
            CallTradePosition = callTradePosition,
            TradePositionChangeSource = TradePositionChangeSourceType.SpreadDistributionStatistics,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

}
