using Newtonsoft.Json;
using System;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;  

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// domain event collection
/// </summary>
[JsonArray(true)]
public class DomainEventCollection : List<IEvent>, ICollection<IEvent>
{

    /// <summary>
    /// domain event collection constructor
    /// </summary>
    public DomainEventCollection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventCollection"/> class with the specified domain events.
    /// </summary>
    /// <remarks>This constructor adds each non-null event from the provided collection to the <see
    /// cref="DomainEventCollection"/>.</remarks>
    /// <param name="domainEvents">A collection of domain events to initialize the collection with. Events that are <see langword="null"/> are
    /// ignored.</param>
    public DomainEventCollection(IEnumerable<IEvent> domainEvents)
    {
        if (domainEvents is not null)
            foreach (var domainEvent in domainEvents)
                if (domainEvent != null)
                    Add(domainEvent);
    }

    /// <summary>
    /// domain event collection constructor
    /// </summary>
    /// <param name="eventLogs"></param>
    public DomainEventCollection(IEnumerable<EventStreamReadModel> eventLogs)
    {
        if (eventLogs is not null)
            foreach (var e in eventLogs)
                if (e is not null)
                    Add(e.ToDomainEvent());
    }

}
