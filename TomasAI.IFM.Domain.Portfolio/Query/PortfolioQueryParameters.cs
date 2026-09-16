using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Groups the immutable services shared by Portfolio query handlers.</summary>
public sealed class PortfolioQueryParameters
{
    /// <summary>Gets the current Portfolio projection query service.</summary>
    public PortfolioQueryService Service { get; }
    /// <summary>Gets the legacy Portfolio history query service.</summary>
    public LegacyPortfolioHistoryQueryService LegacyHistory { get; }
    /// <summary>Gets the durable Portfolio business-identity allocator.</summary>
    public IPortfolioBusinessIdAllocator IdentityAllocator { get; }

    /// <summary>Creates handler parameters from the typed actor context.</summary>
    public PortfolioQueryParameters(Actor.IPortfolioQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IdentityAllocator = context.IdentityAllocator;
        Service = new PortfolioQueryService(context.DbFactory.PortfolioDb, new PortfolioFundStrategyResolver(), context.IdentityAllocator);
        LegacyHistory = new LegacyPortfolioHistoryQueryService(
            new LegacyPortfolioHistoryStore(context.DbFactory.FundLegacyDb, context.DbFactory.TradeDb),
            context.DbFactory.PortfolioDb,
            context.IdentityAllocator);
    }
}