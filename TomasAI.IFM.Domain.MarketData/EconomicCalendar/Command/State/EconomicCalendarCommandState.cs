using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;

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
    readonly Dictionary<EconomicCalendarId, EconomicCalendarReadModel> _economicCalendars = [];

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
        switch (domainEvent)
        {
            case EconomicCalendarAddedEvent added when added.EconomicCalendar is not null:
                return _economicCalendars.TryAdd(added.EntityId, added.EconomicCalendar);
            case EconomicCalendarChangedEvent changed when changed.EconomicCalendar is not null:
                if (!_economicCalendars.ContainsKey(changed.EconomicCalendar.Id)) return false;
                _economicCalendars[changed.EconomicCalendar.Id] = changed.EconomicCalendar;
                return true;
            case EconomicCalendarRemovedEvent removed when removed.EntityId is not null:
                return _economicCalendars.Remove(removed.EntityId);
            case EconomicCalendarsImportedEvent:
                // Import is an operation marker; external records are projected separately.
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Determines whether the specified economic calendar exists in the current state.
    /// </summary>
    /// <param name="economicCalendarId">The unique identifier of the economic calendar to check.</param>
    /// <returns>true if the economic calendar exists in the state; otherwise, false.</returns>
    public bool EconomicCalendarExists(EconomicCalendarId economicCalendarId)
        => _economicCalendars.ContainsKey(economicCalendarId);

    internal int Count => _economicCalendars.Count;

    internal void CopyEconomicCalendarsTo(EconomicCalendarReadModel[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length < _economicCalendars.Count)
            throw new ArgumentException("The destination is too small.", nameof(destination));

        var index = 0;
        foreach (var economicCalendar in _economicCalendars.Values)
            destination[index++] = economicCalendar;
    }

}
