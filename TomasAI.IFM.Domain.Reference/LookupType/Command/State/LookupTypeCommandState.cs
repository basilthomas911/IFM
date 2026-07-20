using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.State;

/// <summary>
/// Represents the persisted state and event application logic for a lookup type actor, including tracking lookup type
/// entities within the actor's lifecycle.
/// </summary>
/// <remarks>This state class is used by event-sourced lookup type actors to manage and apply domain events related to
/// lookup types. It provides methods to check for the existence of lookup type entities and to apply state changes in
/// response to domain events. This type is intended for use within the actor framework and is not typically used
/// directly by application code.</remarks>
public class LookupTypeCommandState
    : BaseEventSourceActorState<LookupTypeCommandState>, IEventSourceActorState<LookupTypeCommandState>
{
    Dictionary<LookupTypeId, LookupTypeReadModel> _lookupTypes = [];

    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Applies state change events to the current lookup type state.
    /// </summary>
    /// <remarks>This method processes domain events and applies the corresponding state changes. Supported events
    /// include adding, changing, and removing lookup types. Events that do not match any known type are ignored and
    /// return false.</remarks>
    /// <param name="domainEvent">The domain event to apply to the state. Cannot be null.</param>
    /// <returns>true if the event was successfully applied; otherwise, false.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                LookupTypeAddedEvent e => On(e),
                LookupTypeChangedEvent e => On(e),
                LookupTypeRemovedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Determines whether the specified lookup type exists in the current state.
    /// </summary>
    /// <param name="lookupTypeId">The unique identifier of the lookup type to check.</param>
    /// <returns>true if the lookup type exists in the state; otherwise, false.</returns>
    public bool LookupTypeExists(LookupTypeId lookupTypeId)
        => _lookupTypes.ContainsKey(lookupTypeId);

    /// <summary>
    /// Applies a lookup type added event to the state.
    /// </summary>
    /// <remarks>This method attempts to add the lookup type from the event to the internal dictionary.
    /// If the lookup type already exists or if the event data is null, the operation fails.</remarks>
    /// <param name="e">The event containing the lookup type to add. Cannot be null.</param>
    /// <returns>true if the lookup type was successfully added to the state; otherwise, false.</returns>
    bool On(LookupTypeAddedEvent e)
    {
        if (e is not null && e.LookupType is not null)
        {
            return _lookupTypes.TryAdd(e.LookupType.Id, e.LookupType);
        }
        return false;
    }

    /// <summary>
    /// Applies a lookup type changed event to the state.
    /// </summary>
    /// <remarks>This method updates the lookup type in the internal dictionary if it exists.
    /// If the lookup type does not exist or if the event data is null, the operation fails.</remarks>
    /// <param name="e">The event containing the updated lookup type. Cannot be null.</param>
    /// <returns>true if the lookup type was successfully updated in the state; otherwise, false.</returns>
    bool On(LookupTypeChangedEvent e)
    {
        if (e is not null && e.LookupType is not null)
        {
            if (_lookupTypes.ContainsKey(e.LookupType.Id))
            {
                _lookupTypes[e.LookupType.Id] = e.LookupType;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Applies a lookup type removed event to the state.
    /// </summary>
    /// <remarks>This method removes the lookup type from the internal dictionary using the provided identifier.
    /// If the lookup type does not exist or if the event data is null, the operation fails.</remarks>
    /// <param name="e">The event containing the identifier of the lookup type to remove. Cannot be null.</param>
    /// <returns>true if the lookup type was successfully removed from the state; otherwise, false.</returns>
    bool On(LookupTypeRemovedEvent e)
    {
        if (e is not null && e.LookupTypeId is not null)
        {
            return _lookupTypes.Remove(e.LookupTypeId);
        }
        return false;
    }
}