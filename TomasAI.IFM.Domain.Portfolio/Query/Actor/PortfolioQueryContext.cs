using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Query.Actor;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class PortfolioQueryContext : QueryActorContext, IPortfolioQueryContext
{
    public PortfolioQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IPortfolioBusinessIdAllocator identityAllocator,
        ILogger<PortfolioQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, PortfolioQueryActor.ActorName))
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(logger);
        DbFactory = dbFactory;
        IdentityAllocator = identityAllocator ?? throw new ArgumentNullException(nameof(identityAllocator));
        Logger = logger;
    }

    public IDbContextFactory DbFactory { get; }
    public IPortfolioBusinessIdAllocator IdentityAllocator { get; }
    public ILogger<PortfolioQueryActor> Logger { get; }
}
