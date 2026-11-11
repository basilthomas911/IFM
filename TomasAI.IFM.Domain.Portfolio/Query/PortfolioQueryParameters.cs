using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Groups the immutable services shared by Portfolio query handlers.</summary>
public sealed class PortfolioQueryParameters
{
    /// <summary>Gets the current Portfolio projection query service.</summary>
    public PortfolioQueryService Service { get; }
    /// <summary>Reads Fund-scoped Databento contracts from projected stores.</summary>
    public FundBaseContractQueryService BaseContracts { get; }
    /// <summary>Creates handler parameters from the typed actor context.</summary>
    /// <param name="context">The validated parent Portfolio query actor context.</param>
    public PortfolioQueryParameters(Actor.IPortfolioQueryContext context)
        : this(context?.DbFactory ?? throw new ArgumentNullException(nameof(context)))
    {
    }

    /// <summary>Creates handler parameters for an owning child query actor.</summary>
    /// <param name="dbFactory">The Portfolio database-context factory.</param>
    public PortfolioQueryParameters(TomasAI.IFM.Application.Storage.IDbContextFactory dbFactory, TomasAI.IFM.Domain.MarketData.Shared.IValueDateProvider? valueDateProvider = null)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        BaseContracts = new(dbFactory.PortfolioDb, dbFactory.SecuritiesDb, dbFactory.ReferenceDb, valueDateProvider);
        Service = new PortfolioQueryService(dbFactory.PortfolioDb, new PortfolioFundStrategyResolver());
    }
}
