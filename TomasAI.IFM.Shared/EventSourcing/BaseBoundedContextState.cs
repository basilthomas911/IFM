using System.Reflection;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Represents a base class for managing the state of a bounded context in a domain-driven design.
/// </summary>
/// <remarks>This abstract class provides mechanisms to apply domain events to update the state and manage a
/// collection of such events. It includes functionality to replay events to reconstruct the state and to apply new
/// events, optionally associating them with commands.</remarks>
/// <typeparam name="TState">The type of the state, which must implement <see cref="IBoundedContextState{TState}"/>.</typeparam>
public abstract class BaseBoundedContextState<TState> : IBoundedContextState<TState> 
    where TState : class, IBoundedContextState<TState>
{
    DomainEventCollection _domainEvents = [];
    public DomainEventCollection Events => _domainEvents!;

    protected abstract bool Apply(IEvent domainEvent);
    public bool Updated { get; private set; }

    /// <summary>
    /// Replays a collection of domain events to rebuild the current state of the object.
    /// </summary>
    /// <remarks>This method processes each event in the provided collection to reconstruct the object's
    /// state.  After replaying, the collection is cleared, and the internal state is reset.  If the collection is null
    /// or empty, the method performs no action.</remarks>
    /// <param name="domainEvents">A collection of domain events to be replayed. The collection must not be null and should contain at least one
    /// event.</param>
    public void ReplayEvents(DomainEventCollection domainEvents)
    {
        if (domainEvents is null || domainEvents.Count == 0) 
            return;
        CreateState();
        domainEvents.ForEach(e => Apply(e, false));
        domainEvents.Clear();
        _domainEvents = [];
        Updated = false;
    }

    /// <summary>
    /// Replays a collection of domain events to reconstruct the current state.
    /// </summary>
    /// <remarks>This method processes each event in the provided collection, applying it to the current
    /// state. Events that are null or cannot be converted to a domain event are ignored. After processing, the internal
    /// list of domain events is cleared, and the updated state is reset.</remarks>
    /// <param name="domainEvents">A collection of <see cref="EventStreamReadModel"/> instances representing the domain events to replay. The
    /// collection must not be null or empty.</param>
    public void ReplayEvents(ICollection<EventStreamReadModel> domainEvents)
    {   
        if (domainEvents is null || domainEvents.Count == 0) 
            return;
        CreateState();
        foreach (var e in domainEvents)
        {
            if (e is not null && e.ToDomainEvent() is IEvent @event)
                Apply(@event, false);
        }
        _domainEvents = [];
        Updated = false;
    }

    /// <summary>
    /// Replays a sequence of domain events to reconstruct the current state.
    /// </summary>
    /// <remarks>If <paramref name="eventStream"/> is <see langword="null"/>, the method returns without
    /// making any changes. The method initializes the state before processing the events and resets the domain events
    /// and update status after replaying.</remarks>
    /// <param name="eventStream">The sequence of events to replay. Each event in the sequence is converted to a domain event and applied to the
    /// current state.</param>
    public void ReplayEvents(IEnumerable<EventStreamReadModel> eventStream)
    {
        if (eventStream is null)
            return;
        CreateState();
        foreach (var e in eventStream)
        {
            if (e is not null && e.ToDomainEvent() is IEvent @event)
                Apply(@event, false);
        }
        _domainEvents = [];
        Updated = false;
    }

    /// <summary>
    /// Replays a sequence of domain events to reconstruct the current state.
    /// </summary>
    /// <remarks>This method processes each event in the provided sequence to rebuild the state of the domain.
    /// Events that are <see langword="null"/> are ignored. After replaying the events, the state is marked as not
    /// updated.</remarks>
    /// <param name="eventStream">The sequence of events to replay. If <see langword="null"/>, no action is taken.</param>
    public void ReplayEvents(IEnumerable<IEvent> eventStream)
    {
        _domainEvents = [];
        if (eventStream is null)
            return;
        CreateState();
        foreach (var @event in eventStream)
        {
            if (@event is not null)
                Apply(@event, false);
        }
        Updated = false;
    }

    /// <summary>
    /// Applies the specified domain event to the current instance and optionally adds it to the event collection.
    /// </summary>
    /// <remarks>If <paramref name="domainEvent"/> is <see langword="null"/> or does not implement <see
    /// cref="IEvent"/>,  the method returns <see langword="false"/> without making any changes. If the event is
    /// successfully applied, the <c>Updated</c> property is set to <see langword="true"/>.</remarks>
    /// <typeparam name="TEvent">The type of the domain event, which must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="domainEvent">The domain event to apply. Can be <see langword="null"/>.</param>
    /// <param name="addEvent">A value indicating whether the domain event should be added to the internal event collection.  Defaults to <see
    /// langword="true"/>.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    public bool Apply<TEvent>(TEvent? domainEvent, bool addEvent = true) where TEvent : IEvent
    {
        var eventApplied = false;
        if (domainEvent is not null and IEvent @event)
        {
            eventApplied = Apply(@event);
            if (addEvent)
                _domainEvents.Add(@event);
        }
        if (eventApplied)
            Updated = true;
        return eventApplied;
    }

    /// <summary>
    /// Updates the state of the system by applying the specified domain event and associating it with the given
    /// command.
    /// </summary>
    /// <remarks>If <paramref name="domainEvent"/> is <see langword="null"/>, the method returns <see
    /// langword="false"/> without performing any updates.</remarks>
    /// <param name="domainEvent">The domain event to be applied. Must not be <see langword="null"/>.</param>
    /// <param name="command">The command associated with the domain event. Must not be <see langword="null"/>.</param>
    /// <param name="addEvent">A value indicating whether the domain event should be added to the event store. Defaults to <see
    /// langword="true"/>.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    public bool Update(IEvent domainEvent, ICommand command, bool addEvent = true)
    {
        if (domainEvent is not null)
        {
            EventModelActor.EventInitHelper.SetProperty(domainEvent, nameof(IEvent.CommandId), command.CommandId);
            return Apply(domainEvent.RoutedFrom(command), addEvent);
        }
        return false;
    }

    /// <summary>
    /// Invokes a non-public parameterless method named "Create" on the current instance, if it exists.
    /// </summary>
    /// <remarks>This method uses reflection to locate and invoke a method named "Create" with no parameters 
    /// and non-public visibility on the current instance. If such a method is not found, no action is taken.</remarks>
    void CreateState() 
    {
        var entityType = GetType();
        var methodName = $"Create";
        var methodInfo = entityType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        methodInfo?.Invoke(this, null);
    }

   
}
