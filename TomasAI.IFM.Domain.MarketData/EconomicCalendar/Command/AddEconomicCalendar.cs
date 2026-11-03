using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;

/// <summary>Handles AddEconomicCalendar through computation, failure guards and state-owned event application.</summary>
public static class AddEconomicCalendar
{

    /// <param name="command">
    /// The concrete EconomicCalendar intent.
    /// The proposed EconomicCalendar change.
    /// The originating command and route.
    /// </param>
    extension(AddEconomicCalendarCommand command)
    {
        /// <summary>Applies the computed business data only when ownership and lifecycle guards succeed.</summary>
        ///
        /// <param name="state">The authoritative EconomicCalendar state.</param>
        /// <returns>The command ID on success, or the guarded/application failure.</returns>
        public ServiceResult<GuidResult> Execute(EconomicCalendarCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = "EconomicCalendar.STATE.APPLY_FAILED";
            var computed = command.Compute(out var economicCalendar);
            var updated = computed switch
            {
                _ when !computed => command.UpdateFailed(ref errorMsg, "EconomicCalendar.COMPUTED_DATA.INVALID"),
                _ when state.EconomicCalendarExists(command.EntityId)
                    => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: economic calendar {command.EntityId} already exists"),
                _ => state.Update(command.CreateEconomicCalendarAddedEvent(economicCalendar), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes business data without modifying actor state or command inputs.</summary>
        ///
        /// <param name="economicCalendar">The computed business data to carry in its event.</param>
        /// <returns>True when the computed data belongs to the commanded entity.</returns>
        internal bool Compute(out EconomicCalendarReadModel economicCalendar)
        {
            economicCalendar = command.EconomicCalendar is { } proposedEconomicCalendar ? proposedEconomicCalendar with { } : null!;
            return economicCalendar is not null && economicCalendar.IsValid && !string.IsNullOrWhiteSpace(economicCalendar.EventName) && economicCalendar.Id == command.EntityId;
        }

        /// <summary>Creates the private event from guarded business data without changing state.</summary>
        ///
        /// <param name="economicCalendar">The computed, guarded business data.</param>
        /// <returns>The source event applied and persisted through State.Update.</returns>
        internal EconomicCalendarAddedEvent CreateEconomicCalendarAddedEvent(EconomicCalendarReadModel economicCalendar) => new()
        {
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, EconomicCalendarAddedEvent.Actor, EconomicCalendarAddedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            EconomicCalendar = economicCalendar,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
    }
}
