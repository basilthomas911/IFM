using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;

/// <summary>Handles InsertFuturesBarData commands through computation and state-owned event application.</summary>
public static class InsertFuturesBarData
{

    /// <param name="command">
    /// The originating command and its business inputs.
    /// The command supplying the requested business values.
    /// The originating command supplying route and audit metadata.
    /// </param>
    extension(InsertFuturesBarDataCommand command)
    {
        /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
        ///
        /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
        /// <returns>The command identity on success, or the business rejection or application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesBarDataCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply FuturesBarDataInsertedEvent";
            var updated = command.Compute(out var futuresBarDataInsertion) switch
            {
                _ => state.Update(command.CreateFuturesBarDataInsertedEvent(futuresBarDataInsertion), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
        ///
        /// <param name="futuresBarDataInsertion">The proposed business values passed to the event factory.</param>
        /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
        internal bool Compute(out FuturesBarDataInsertion futuresBarDataInsertion)
        {
            futuresBarDataInsertion = new(command.FuturesBarData);
            return true;
        }

        /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
        ///
        /// <param name="futuresBarDataInsertion">The accepted business values to carry in the event.</param>
        /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
        internal FuturesBarDataInsertedEvent CreateFuturesBarDataInsertedEvent(FuturesBarDataInsertion futuresBarDataInsertion)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, FuturesBarDataInsertedEvent.Actor, FuturesBarDataInsertedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                FuturesBarData = futuresBarDataInsertion.FuturesBarData,
                CreatedOn = command.OriginatedOn,
                CreatedBy = command.OriginatedBy
            };
    }
}
