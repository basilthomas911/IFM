using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command;

/// <summary>Handles StartMarketDataFeed commands through computation and state-owned event application.</summary>
public static class StartMarketDataFeed
{

    /// <param name="command">
    /// The originating command and its business inputs.
    /// The command supplying the requested business values.
    /// The originating command supplying route and audit metadata.
    /// </param>
    extension(StartMarketDataFeedCommand command)
    {
        /// <summary>Applies the prepared start without consulting the backend.</summary>
        public ServiceResult<GuidResult> Execute(MarketDataFeedCommandState state) =>
            command.Execute(state, command.PreparedContracts ?? []);

        /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
        ///
        /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
        /// <param name="preparedContracts">The API-owned subscriptions resolved for the captured session.</param>
        /// <returns>The command identity on success, or the business rejection or application failure.</returns>
        public ServiceResult<GuidResult> Execute(MarketDataFeedCommandState state, FuturesContractV3ReadModel[] preparedContracts)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            if (preparedContracts is not { Length: > 0 })
                return new ServiceFailed<GuidResult>(command.ErrorCode, "The API has no prepared market-data contract assignments.");
            var errorMsg = $"{command.CommandName}: unable to apply MarketDataFeedStartedEvent";
            var updated = command.Compute(preparedContracts, out var marketDataFeedStart) switch
            {
                _ => state.Update(command.CreateMarketDataFeedStartedEvent(marketDataFeedStart), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
        ///
        /// <param name="preparedContracts">The backend subscription snapshot.</param>
        /// <param name="marketDataFeedStart">The proposed business values passed to the event factory.</param>
        /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
        internal bool Compute(FuturesContractV3ReadModel[] preparedContracts, out MarketDataFeedStart marketDataFeedStart)
        {
            marketDataFeedStart = new(preparedContracts, command.ValueDate, false);
            return true;
        }

        /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
        ///
        /// <param name="marketDataFeedStart">The accepted business values to carry in the event.</param>
        /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
        internal MarketDataFeedStartedEvent CreateMarketDataFeedStartedEvent(MarketDataFeedStart marketDataFeedStart)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, MarketDataFeedStartedEvent.Actor, MarketDataFeedStartedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                FuturesContracts = marketDataFeedStart.FuturesContracts,
                ValueDate = marketDataFeedStart.ValueDate,
                ResetStream = marketDataFeedStart.ResetStream,
                StartedOn = command.OriginatedOn,
                StartedBy = command.OriginatedBy
            };
    }
}
