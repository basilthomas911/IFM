using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Groups the immutable services shared by Portfolio query handlers.</summary>
public sealed class PortfolioQueryParameters
{
    /// <summary>Gets the current Portfolio projection query service.</summary>
    public PortfolioQueryService Service { get; }
    /// <summary>Gets the durable Portfolio business-identity allocator.</summary>
    public IPortfolioBusinessIdAllocator IdentityAllocator { get; }

    /// <summary>Creates handler parameters from the typed actor context.</summary>
    /// <param name="context">The validated parent Portfolio query actor context.</param>
    public PortfolioQueryParameters(Actor.IPortfolioQueryContext context)
        : this(context?.DbFactory ?? throw new ArgumentNullException(nameof(context)), context.IdentityAllocator)
    {
    }

    /// <summary>Creates handler parameters for an owning child query actor.</summary>
    /// <param name="dbFactory">The Portfolio database-context factory.</param>
    /// <param name="identityAllocator">The durable business-identity allocator.</param>
    public PortfolioQueryParameters(TomasAI.IFM.Application.Storage.IDbContextFactory dbFactory, IPortfolioBusinessIdAllocator identityAllocator)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        IdentityAllocator = identityAllocator ?? throw new ArgumentNullException(nameof(identityAllocator));
        Service = new PortfolioQueryService(dbFactory.PortfolioDb, new PortfolioFundStrategyResolver(), identityAllocator);
    }
}
