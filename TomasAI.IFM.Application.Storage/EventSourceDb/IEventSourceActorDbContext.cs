using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// Combines actor event-source database read and write capabilities.
/// </summary>
public interface IEventSourceActorDbContext :
    IObjectRepository<EventSourceDb.EventSourceActorDbContext>,
    EventSourceDb.IEventSourceActorDbReadContext,
    EventSourceDb.IEventSourceActorDbWriteContext
{
    /// <summary>Business clock shared by persistence and cold projector replay; IO leases remain physical.</summary>
    TimeProvider BusinessClock => TimeProvider.System;
    /// <summary>Gets the actor event-source database read capability.</summary>
    EventSourceDb.IEventSourceActorDbReadContext DbReader { get; }

    /// <summary>Gets the actor event-source database write capability.</summary>
    EventSourceDb.IEventSourceActorDbWriteContext DbWriter { get; }
}
