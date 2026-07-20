using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.State;

/// <summary>
/// Represents the persisted state and event application logic for an economic calendar actor, including tracking economic calendar
/// entities within the actor's lifecycle.
/// </summary>
/// <remarks>This state class is used by event-sourced economic calendar actors to manage and apply domain events related to
/// economic calendars. It provides methods to check for the existence of economic calendar entities and to apply state changes in
/// response to domain events. This type is intended for use within the actor framework and is not typically used
/// directly by application code.</remarks>
public class EconomicCalendarCommandState
    : BaseEventSourceActorState<EconomicCalendarCommandState>, IEventSourceActorState<EconomicCalendarCommandState>
{
    Dictionary<EconomicCalendarId, EconomicCalendarReadModel> _economicCalendars = [];

    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Applies state change events to the current economic calendar state.
    /// </summary>
    /// <remarks>This method processes domain events and applies the corresponding state changes. Supported events
    /// include adding, changing, and removing economic calendars. Events that do not match any known type are ignored and
    /// return false.</remarks>
    /// <param name="domainEvent">The domain event to apply to the state. Cannot be null.</param>
    /// <returns>true if the event was successfully applied; otherwise, false.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                EconomicCalendarAddedEvent e => On(e),
                EconomicCalendarChangedEvent e => On(e),
                EconomicCalendarRemovedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Determines whether the specified economic calendar exists in the current state.
    /// </summary>
    /// <param name="economicCalendarId">The unique identifier of the economic calendar to check.</param>
    /// <returns>true if the economic calendar exists in the state; otherwise, false.</returns>
    public bool EconomicCalendarExists(EconomicCalendarId economicCalendarId)
        => _economicCalendars.ContainsKey(economicCalendarId);

    /// <summary>
    /// Applies an economic calendar added event to the state.
    /// </summary>
    /// <remarks>This method attempts to add the economic calendar from the event to the internal dictionary.
    /// If the economic calendar already exists or if the event data is null, the operation fails.</remarks>
    /// <param name="e">The event containing the economic calendar to add. Cannot be null.</param>
    /// <returns>true if the economic calendar was successfully added to the state; otherwise, false.</returns>
    bool On(EconomicCalendarAddedEvent e)
    {
        if (e is not null && e.EconomicCalendar is not null)
        {
            return _economicCalendars.TryAdd(e.EconomicCalendar.Id, e.EconomicCalendar);
        }
        return false;
    }

    /// <summary>
    /// Applies an economic calendar changed event to the state.
    /// </summary>
    /// <remarks>This method updates the economic calendar in the internal dictionary if it exists.
    /// If the economic calendar does not exist or if the event data is null, the operation fails.</remarks>
    /// <param name="e">The event containing the updated economic calendar. Cannot be null.</param>
    /// <returns>true if the economic calendar was successfully updated in the state; otherwise, false.</returns>
    bool On(EconomicCalendarChangedEvent e)
    {
        if (e is not null && e.EconomicCalendar is not null)
        {
            if (_economicCalendars.ContainsKey(e.EconomicCalendar.Id))
            {
                _economicCalendars[e.EconomicCalendar.Id] = e.EconomicCalendar;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Applies an economic calendar removed event to the state.
    /// </summary>
    /// <remarks>This method removes the economic calendar from the internal dictionary using the provided identifier.
    /// If the economic calendar does not exist or if the event data is null, the operation fails.</remarks>
    /// <param name="e">The event containing the identifier of the economic calendar to remove. Cannot be null.</param>
    /// <returns>true if the economic calendar was successfully removed from the state; otherwise, false.</returns>
    bool On(EconomicCalendarRemovedEvent e)
    {
        if (e is not null && e.EntityId is not null)
        {
            return _economicCalendars.Remove(e.EntityId);
        }
        return false;
    }
}
