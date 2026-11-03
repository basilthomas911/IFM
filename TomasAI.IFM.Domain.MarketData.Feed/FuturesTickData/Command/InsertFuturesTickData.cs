using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command;

/// <summary>Handles InsertFuturesTickData commands through computation and state-owned event application.</summary>
public static class InsertFuturesTickData
{

    /// <param name="command">
    /// The originating command and its business inputs.
    /// The command supplying the requested business values.
    /// The originating command supplying route and audit metadata.
    /// </param>
    extension(InsertFuturesTickDataCommand command)
    {
        /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
        ///
        /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
        /// <returns>The command identity on success, or the business rejection or application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesTickDataCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply FuturesTickDataInsertedEvent";
            var updated = command.Compute(out var futuresTickDataInsertion) switch
            {
                _ => state.Update(command.CreateFuturesTickDataInsertedEvent(futuresTickDataInsertion), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
        ///
        /// <param name="futuresTickDataInsertion">The proposed business values passed to the event factory.</param>
        /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
        internal bool Compute(out FuturesTickDataInsertion futuresTickDataInsertion)
        {
            futuresTickDataInsertion = new(command.Contract, command.TickData);
            return true;
        }

        /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
        ///
        /// <param name="futuresTickDataInsertion">The accepted business values to carry in the event.</param>
        /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
        internal FuturesTickDataInsertedEvent CreateFuturesTickDataInsertedEvent(FuturesTickDataInsertion futuresTickDataInsertion)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, FuturesTickDataInsertedEvent.Actor, FuturesTickDataInsertedEvent.Verb, command.EntityId.Format()),
                EntityId = new(futuresTickDataInsertion.FuturesTickData.ContractId, futuresTickDataInsertion.FuturesTickData.ValueDate, futuresTickDataInsertion.FuturesTickData.TickId),
                Contract = futuresTickDataInsertion.FuturesContract,
                TickData = futuresTickDataInsertion.FuturesTickData,
                CreatedOn = command.OriginatedOn,
                CreatedBy = command.OriginatedBy
            };
    }
}
