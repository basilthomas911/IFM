using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command;

/// <summary>Handles InsertFuturesEodData commands through computation and state-owned event application.</summary>
public static class InsertFuturesEodData
{

    /// <param name="command">
    /// The originating command and its business inputs.
    /// The command supplying the requested business values.
    /// The originating command supplying route and audit metadata.
    /// </param>
    extension(InsertFuturesEodDataCommand command)
    {
        /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
        ///
        /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
        /// <returns>The command identity on success, or the business rejection or application failure.</returns>
        /// <exception cref="ArgumentNullException">Required tick, contract, or current EOD inputs are absent.</exception>
        public ServiceResult<GuidResult> Execute(FuturesEodDataCommandState state, IMarketSessionCalendar? calendar = null)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply FuturesEodDataInsertedEvent";
            var updated = command.Compute(out var futuresEodData) switch
            {
                _ => state.Update(command.CreateFuturesEodDataInsertedEvent(futuresEodData, calendar), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
        ///
        /// <param name="futuresEodData">The proposed business values passed to the event factory.</param>
        /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
        /// <exception cref="ArgumentNullException">Required tick, contract, or current EOD inputs are absent.</exception>
        internal bool Compute(out FuturesEodDataV2ReadModel futuresEodData)
        {
            futuresEodData = FuturesEodDataModel.CreateFuturesEodData(
                command.ValueDate, command.FuturesTickData, command.Contract, command.EodDataToday,
                command.EodDataRange, command.NormCurveData, command.WindowSize, command.VixEodData);
            return true;
        }

        /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
        ///
        /// <param name="futuresEodData">The accepted business values to carry in the event.</param>
        /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
        internal FuturesEodDataInsertedEvent CreateFuturesEodDataInsertedEvent(FuturesEodDataV2ReadModel futuresEodData, IMarketSessionCalendar? calendar = null)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, FuturesEodDataInsertedEvent.Actor, FuturesEodDataInsertedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                FuturesEodData = futuresEodData,
                RawObservation = calendar is null ? null : FuturesEodRawObservationFactory.Create(futuresEodData, 0, calendar),
                CreatedOn = command.OriginatedOn,
                CreatedBy = command.OriginatedBy
            };
    }
}
