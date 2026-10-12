using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;
/// <summary>Provides the closed-generic Query context for Regime Discovery.</summary>
public sealed class RegimeDiscoveryQueryContext : QueryActorContext, IRegimeDiscoveryQueryContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    /// <summary>Initializes the Regime Discovery Query context.</summary>
    public RegimeDiscoveryQueryContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, ILogger<RegimeDiscoveryQueryActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Query, RegimeDiscoveryQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<RegimeDiscoveryQueryActor> Logger { get; }
}
