using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

/// <summary>Handles RemoveTradeLiveFeed commands through computation and state-owned event application.</summary>
public static class RemoveTradeLiveFeed
{

    /// <param name="command">
    /// The originating command and its business inputs.
    /// The command supplying the requested business values.
    /// The originating command supplying route and audit metadata.
    /// </param>
    extension(RemoveTradeLiveFeedCommand command)
    {
        /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
        ///
        /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
        /// <returns>The command identity on success, or the business rejection or application failure.</returns>
        public ServiceResult<GuidResult> Execute(MarketDataFeedCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply TradeLiveFeedRemovedEvent";
            var updated = command.Compute(state.IsTradeLiveFeedOn, out var tradeLiveFeedRemoval) switch
            {
                _ when !tradeLiveFeedRemoval.Accepted
                    => command.UpdateFailed(ref errorMsg, $"Trade live feed is already off for: {command.OrderId}:{command.TradeId}"),
                _ => state.Update(command.CreateTradeLiveFeedRemovedEvent(tradeLiveFeedRemoval), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
        ///
        /// <param name="isTradeLiveFeedOn">Whether the owning state currently has an active trade feed.</param>
        /// <param name="tradeLiveFeedRemoval">The proposed business values passed to the event factory.</param>
        /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
        internal bool Compute(bool isTradeLiveFeedOn, out TradeLiveFeedRemoval tradeLiveFeedRemoval)
        {
            tradeLiveFeedRemoval = new(command.OrderId, command.TradeId, TradeLiveFeedStateType.Off, isTradeLiveFeedOn);
            return true;
        }

        /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
        ///
        /// <param name="tradeLiveFeedRemoval">The accepted business values to carry in the event.</param>
        /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
        internal TradeLiveFeedRemovedEvent CreateTradeLiveFeedRemovedEvent(TradeLiveFeedRemoval tradeLiveFeedRemoval)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, TradeLiveFeedRemovedEvent.Actor, TradeLiveFeedRemovedEvent.Verb, command.EntityId.Format()),
                EntityId = new TradeLiveFeedId(command.OrderId, command.TradeId, command.ValueDate),
                OrderId = tradeLiveFeedRemoval.OrderId,
                TradeId = tradeLiveFeedRemoval.TradeId,
                TradeLiveFeedState = tradeLiveFeedRemoval.TradeLiveFeedState,
                RemovedOn = command.OriginatedOn,
                RemovedBy = command.OriginatedBy
            };
    }
}
