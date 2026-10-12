using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="YieldCurveRateCommandActor"/>.</summary>
public sealed class YieldCurveRateCommandContext : CommandActorContext,
IYieldCurveRateCommandContext
{
    readonly Lazy<IEventSourceActorDbContext> _dbEventSource;
    readonly Lazy<IEventSourceActorStateFactory> _stateFactory;
    readonly Lazy<IActorService> _actorService;
    /// <summary>Initializes the context.</summary>
    public YieldCurveRateCommandContext(IActorSupervisor supervisor, IDbContextFactory dbFactory,
        ILogger<YieldCurveRateCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, YieldCurveRateCommandActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory); Logger = IsArgumentNull.Set(logger);
        _dbEventSource = ResolveOnce<IEventSourceActorDbContext>();
        _stateFactory = ResolveOnce<IEventSourceActorStateFactory>();
        _actorService = ResolveOnce<IActorService>();
    }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<YieldCurveRateCommandActor> Logger { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource => _dbEventSource.Value;
    /// <inheritdoc/>
    public IEventSourceActorStateFactory StateFactory => _stateFactory.Value;
    /// <inheritdoc/>
    public IActorService ActorService => _actorService.Value;
    Lazy<T> ResolveOnce<T>() where T : class => new(() => IsArgumentNull.Set(Container.Resolve<T>())!);
}
