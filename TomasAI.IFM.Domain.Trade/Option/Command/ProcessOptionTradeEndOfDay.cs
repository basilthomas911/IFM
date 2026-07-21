using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;
using TomasAI.IFM.Domain.Trade.Option.Command.State;

namespace TomasAI.IFM.Domain.Trade.Option.Command;

internal static class ProcessOptionTradeEndOfDay
{
    /// <summary>
    /// Executes end-of-day processing for an option trade command with validation checks.
    /// </summary>
    /// <param name="e">The command containing end-of-day processing details.</param>
    /// <param name="state">The current state of the option trade command.</param>
    /// <returns>True if the end-of-day processing completed successfully and the state was updated; otherwise, false.</returns>
    /// <exception cref="ProcessOptionTradeEndOfDayException">Thrown when the trade does not exist, is not in end-of-day status, or is not an Iron Condor trade type.</exception>
    public static bool Execute(this ProcessOptionTradeEndOfDayCommand e, OptionTradeCommandState state)
    {
        return e switch
        {
            _ when !state.TradeExists(e.EntityId) => throw new ProcessOptionTradeEndOfDayException(
                $"{e.CommandName}: trade: {e.OrderId}:{e.TradeId} does not exist"),
            _ when !state.IsTradeInEndOfDayStatus(e.TradeStatus) => throw new ProcessOptionTradeEndOfDayException(
                $"{e.CommandName}: trade: {e.OrderId}:{e.TradeId} status = {e.TradeStatus} - must be in EndOfDay status"),
            _ when !state.IsIronCondorTrade(e.TradeType) => throw new ProcessOptionTradeEndOfDayException(
                $"{e.CommandName} trade: {e.OrderId}:{e.TradeId} trade type {e.TradeType} cannot be processed - must be an Iron Condor trade"),
            _ => ProcessOptionTradeEndOfDay(e, state)
        };

        /// <summary>
        /// Process the end-of-day updates for the option trade.
        /// </summary>
        /// <param name="e">The command containing the end-of-day processing details.</param>
        /// <param name="state">The current state of the option trade.</param>
        /// <returns>True if the state was updated successfully; otherwise, false.</returns>
        static bool ProcessOptionTradeEndOfDay(ProcessOptionTradeEndOfDayCommand e, OptionTradeCommandState state)
        {
            var tradePositionId = new TradePositionEntityId(
                            OrderId: e.OrderId,
                            TradeId: e.TradeId,
                            TradeType: e.TradeType,
                            ValueDate: e.ValueDate,
                            DaysToExpiry: state.DaysToExpiry(e.ValueDate),
                            TradeStatus: e.TradeStatus);
            var tradePnl = state.GetTradePnl(e.ValueDate);

            state.Update(e.CreateOptionTradeEndOfDayProcessedEvent(tradePositionId, tradePnl), e);
            state.Update(e.EntityId.CreateTradePositionStatusUpdatedEvent(e.CommandId, tradePositionId, state.PutSpreadTradeType, e.OriginatedOn, e.OriginatedBy), e);
            state.Update(e.EntityId.CreateTradePositionStatusUpdatedEvent(e.CommandId, tradePositionId, state.CallSpreadTradeType, e.OriginatedOn, e.OriginatedBy), e);
            return state.Update(new SnapshotOptionTradeCommand { OrderId = e.OrderId, TradeId = e.TradeId, CommandId = e.CommandId, EntityId = e.EntityId }
                .CreateOptionTradeSnapshotEvent(state.ToOptionTrade()), e);
        }
    }

    /// <summary>
    /// Creates an <see cref="OptionTradeEndOfDayProcessedEvent"/> from a <see cref="ProcessOptionTradeEndOfDayCommand"/>,
    /// the resolved end-of-day <see cref="TradePositionEntityId"/>, and the calculated trade P&amp;L.
    /// Captures OHLCV price data, fund, order, and reference information from the command.
    /// </summary>
    /// <param name="e">The command carrying the end-of-day OHLCV prices, fund, and reference data.</param>
    /// <param name="tradePositionId">The trade position entity identity used as the EOD key.</param>
    /// <param name="tradePnl">The calculated trade profit and loss for the end-of-day period.</param>
    /// <returns>A fully populated <see cref="OptionTradeEndOfDayProcessedEvent"/>.</returns>
    internal static OptionTradeEndOfDayProcessedEvent CreateOptionTradeEndOfDayProcessedEvent(this ProcessOptionTradeEndOfDayCommand e,
        TradePositionEntityId tradePositionId, decimal tradePnl)
        => new ()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, OptionTradeEndOfDayProcessedEvent.Actor, OptionTradeEndOfDayProcessedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            FundId = e.FundId,
            OrderId = e.OrderId,
            EodKey = tradePositionId,
            OpenPrice = e.OpenPrice,
            HighPrice = e.HighPrice,
            LowPrice = e.LowPrice,
            ClosePrice = e.ClosePrice,
            Volume = e.Volume,
            TradePnl = tradePnl,
            Reference = e.Reference,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Creates a <see cref="TradePositionStatusUpdatedEvent"/> scoped to a specific <see cref="OptionTradeEntityId"/>.
    /// Transitions the trade position status from <see cref="TradeStatus.IntraDay"/> to <see cref="TradeStatus.EndOfDay"/>.
    /// Called once per spread leg (put and call) during end-of-day processing.
    /// </summary>
    /// <param name="entityId">The option trade entity identity that owns the trade position being updated.</param>
    /// <param name="commandId">The identifier of the originating command, used for idempotency tracking.</param>
    /// <param name="tradePositionId">The trade position entity identity supplying order id, trade id, value date, and days to expiry.</param>
    /// <param name="tradeType">The trade type of the leg being transitioned (e.g. put or call spread).</param>
    /// <param name="originatedOn">The timestamp at which the status change was originated.</param>
    /// <param name="originatedBy">The user or system that originated the status change.</param>
    /// <returns>A fully populated <see cref="TradePositionStatusUpdatedEvent"/>.</returns>
    internal static TradePositionStatusUpdatedEvent CreateTradePositionStatusUpdatedEvent(this OptionTradeEntityId entityId,
        Guid commandId, TradePositionEntityId tradePositionId, TradeType tradeType, DateTime originatedOn, string originatedBy)
        => new()
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Event, TradePositionStatusUpdatedEvent.Actor, TradePositionStatusUpdatedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            OrderId = tradePositionId.OrderId,
            TradeId = tradePositionId.TradeId,
            TradeType = tradeType,
            ValueDate = tradePositionId.ValueDate,
            DaysToExpiry = tradePositionId.DaysToExpiry,
            OldTradeStatus = TradeStatus.IntraDay,
            NewTradeStatus = TradeStatus.EndOfDay,
            UpdatedOn = originatedOn,
            UpdatedBy = originatedBy
        };


}
