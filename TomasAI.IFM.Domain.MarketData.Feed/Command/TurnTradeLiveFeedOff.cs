using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

/// <summary>Handles TurnTradeLiveFeedOff commands through computation and state-owned event application.</summary>
public static class TurnTradeLiveFeedOff
{

    /// <param name="command">
    /// The originating command and its business inputs.
    /// The command supplying the requested business values.
    /// The originating command supplying route and audit metadata.
    /// </param>
    extension(TurnTradeLiveFeedOffCommand command)
    {
        /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
        ///
        /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
        /// <returns>The command identity on success, or the business rejection or application failure.</returns>
        public ServiceResult<GuidResult> Execute(MarketDataFeedCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply TradeLiveFeedTurnedOffEvent";
            var updated = command.Compute(state.IsTradeLiveFeedOn, out var tradeLiveFeedDeactivation) switch
            {
                _ when !tradeLiveFeedDeactivation.Accepted
                    => command.UpdateFailed(ref errorMsg, $"Trade live feed is already off for: {command.OrderId}:{command.TradeId}"),
                _ => state.Update(command.CreateTradeLiveFeedTurnedOffEvent(tradeLiveFeedDeactivation), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
        ///
        /// <param name="isTradeLiveFeedOn">Whether the owning state currently has an active trade feed.</param>
        /// <param name="tradeLiveFeedDeactivation">The proposed business values passed to the event factory.</param>
        /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
        internal bool Compute(bool isTradeLiveFeedOn, out TradeLiveFeedDeactivation tradeLiveFeedDeactivation)
        {
            tradeLiveFeedDeactivation = new(command.OrderId, command.TradeId, isTradeLiveFeedOn);
            return true;
        }

        /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
        ///
        /// <param name="tradeLiveFeedDeactivation">The accepted business values to carry in the event.</param>
        /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
        internal TradeLiveFeedTurnedOffEvent CreateTradeLiveFeedTurnedOffEvent(TradeLiveFeedDeactivation tradeLiveFeedDeactivation)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, TradeLiveFeedTurnedOffEvent.Actor, TradeLiveFeedTurnedOffEvent.Verb, command.EntityId.Format()),
                EntityId = new TradeLiveFeedId(command.OrderId, command.TradeId, command.ValueDate),
                OrderId = tradeLiveFeedDeactivation.OrderId,
                TradeId = tradeLiveFeedDeactivation.TradeId,
                UpdatedOn = command.OriginatedOn,
                UpdatedBy = command.OriginatedBy
            };
    }
}
