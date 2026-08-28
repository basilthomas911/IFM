using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor.Templates;

/// <summary>Defines the readonly services required by <see cref="CommandActorTemplate"/>.</summary>
public interface ICommandActorTemplateContext : ICommandActorContext<CommandActorTemplate>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the event-source database context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<CommandActorTemplate> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="CommandActorTemplate"/>.</summary>
public sealed class CommandActorTemplateContext : CommandActorContext, ICommandActorTemplateContext
{
    /// <summary>Initializes a new command template context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="dbEventSource">The event-source database context.</param>
    /// <param name="logger">The actor logger.</param>
    public CommandActorTemplateContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        ILogger<CommandActorTemplate> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, CommandActorTemplate.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource { get; }

    /// <inheritdoc/>
    public ILogger<CommandActorTemplate> Logger { get; }
}

/// <summary>Defines the readonly services required by <see cref="EventActorTemplate"/>.</summary>
public interface IEventActorTemplateContext : IEventActorContext<EventActorTemplate>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<EventActorTemplate> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="EventActorTemplate"/>.</summary>
public sealed class EventActorTemplateContext : EventActorContext, IEventActorTemplateContext
{
    /// <summary>Initializes a new event template context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="logger">The actor logger.</param>
    public EventActorTemplateContext(
        IActorSupervisor supervisor,
        ILogger<EventActorTemplate> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, EventActorTemplate.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public ILogger<EventActorTemplate> Logger { get; }
}

/// <summary>Defines the readonly services required by <see cref="RealtimeActorTemplate"/>.</summary>
public interface IRealtimeActorTemplateContext : IRealtimeActorContext<RealtimeActorTemplate>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<RealtimeActorTemplate> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="RealtimeActorTemplate"/>.</summary>
public sealed class RealtimeActorTemplateContext : EventActorContext, IRealtimeActorTemplateContext
{
    /// <summary>Initializes a new realtime template context.</summary>
    public RealtimeActorTemplateContext(
        IActorSupervisor supervisor,
        ILogger<RealtimeActorTemplate> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, RealtimeActorTemplate.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public ILogger<RealtimeActorTemplate> Logger { get; }
}

/// <summary>Defines the readonly services required by <see cref="QueryActorTemplate"/>.</summary>
public interface IQueryActorTemplateContext : IQueryActorContext<QueryActorTemplate>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<QueryActorTemplate> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="QueryActorTemplate"/>.</summary>
public sealed class QueryActorTemplateContext : QueryActorContext, IQueryActorTemplateContext
{
    /// <summary>Initializes a new query template context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="dbFactory">The database-context factory.</param>
    /// <param name="logger">The actor logger.</param>
    public QueryActorTemplateContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<QueryActorTemplate> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, QueryActorTemplate.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }

    /// <inheritdoc/>
    public ILogger<QueryActorTemplate> Logger { get; }
}
