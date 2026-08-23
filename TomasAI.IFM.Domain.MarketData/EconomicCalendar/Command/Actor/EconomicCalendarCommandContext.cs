using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="EconomicCalendarCommandActor"/>.</summary>
public interface IEconomicCalendarCommandContext : ICommandActorContext<EconomicCalendarCommandActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<EconomicCalendarCommandActor> Logger { get; }
    /// <summary>Gets the event-source database context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="EconomicCalendarCommandActor"/>.</summary>
public sealed class EconomicCalendarCommandContext : CommandActorContext,
    ICommandActorContext<EconomicCalendarCommandActor>, IEconomicCalendarCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;

    /// <summary>Initializes the context.</summary>
    public EconomicCalendarCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        ILogger<EconomicCalendarCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, EconomicCalendarCommandActor.Actor))
    {
        DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<EconomicCalendarCommandActor> Logger { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}
