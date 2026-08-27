using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;

/// <summary>Defines readonly services owned by the Regime Discovery Query actor.</summary>
public interface IRegimeDiscoveryQueryContext : IQueryActorContext<RegimeDiscoveryQueryActor>
{
    /// <summary>Gets the application database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the query actor logger.</summary>
    ILogger<RegimeDiscoveryQueryActor> Logger { get; }
}

/// <summary>Provides the closed-generic Query context for Regime Discovery.</summary>
public sealed class RegimeDiscoveryQueryContext
    : QueryActorContext,
      IQueryActorContext<RegimeDiscoveryQueryActor>,
      IRegimeDiscoveryQueryContext
{
    /// <summary>Initializes the Regime Discovery Query context.</summary>
    public RegimeDiscoveryQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<RegimeDiscoveryQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, RegimeDiscoveryQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc />
    public ILogger<RegimeDiscoveryQueryActor> Logger { get; }
}
