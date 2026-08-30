using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Query.Actor;

public sealed class PortfolioQueryContext : QueryActorContext, IQueryActorContext<PortfolioQueryActor>, IPortfolioQueryContext
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
