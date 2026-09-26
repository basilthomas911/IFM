using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// Combines actor event-source database read and write capabilities.
/// </summary>
public interface IEventSourceActorDbContext :
    IObjectRepository<EventSourceDb.EventSourceActorDbContext>,
    EventSourceDb.IEventSourceActorDbReadContext,
    EventSourceDb.IEventSourceActorDbWriteContext
{
    /// <summary>Gets the actor event-source database read capability.</summary>
    EventSourceDb.IEventSourceActorDbReadContext DbReader { get; }

    /// <summary>Gets the actor event-source database write capability.</summary>
    EventSourceDb.IEventSourceActorDbWriteContext DbWriter { get; }
}
