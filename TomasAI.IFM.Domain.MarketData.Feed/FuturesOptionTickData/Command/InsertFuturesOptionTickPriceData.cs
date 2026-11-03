using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command;

/// <summary>Handles InsertFuturesOptionTickPriceData commands through computation and state-owned event application.</summary>
public static class InsertFuturesOptionTickPriceData
{

    /// <param name="command">
    /// The originating command and its business inputs.
    /// The command supplying the requested business values.
    /// The originating command supplying route and audit metadata.
    /// </param>
    extension(InsertFuturesOptionTickPriceDataCommand command)
    {
        /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
        ///
        /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
        /// <returns>The command identity on success, or the business rejection or application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesOptionTickDataCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply FuturesOptionTickPriceDataInsertedEvent";
            var updated = command.Compute(out var futuresOptionTickPriceDataInsertion) switch
            {
                _ => state.Update(command.CreateFuturesOptionTickPriceDataInsertedEvent(futuresOptionTickPriceDataInsertion), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
        ///
        /// <param name="futuresOptionTickPriceDataInsertion">The proposed business values passed to the event factory.</param>
        /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
        internal bool Compute(out FuturesOptionTickPriceDataInsertion futuresOptionTickPriceDataInsertion)
        {
            futuresOptionTickPriceDataInsertion = new(command.Contract, command.OptionTickData);
            return true;
        }

        /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
        ///
        /// <param name="futuresOptionTickPriceDataInsertion">The accepted business values to carry in the event.</param>
        /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
        internal FuturesOptionTickPriceDataInsertedEvent CreateFuturesOptionTickPriceDataInsertedEvent(FuturesOptionTickPriceDataInsertion futuresOptionTickPriceDataInsertion)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, FuturesOptionTickPriceDataInsertedEvent.Actor, FuturesOptionTickPriceDataInsertedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                Contract = futuresOptionTickPriceDataInsertion.FuturesContract,
                TickData = futuresOptionTickPriceDataInsertion.FuturesOptionTickData,
                CreatedOn = command.OriginatedOn,
                CreatedBy = command.OriginatedBy
            };
    }
}
