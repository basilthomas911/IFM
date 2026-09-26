namespace TomasAI.IFM.Domain.Portfolio.Shared.Queries;

/// <summary>Canonical actor names for the Portfolio-owned query routes.</summary>
public static class PortfolioQueryRoutes
{
    /// <summary>The Portfolio root query route.</summary>
    public const string Portfolio = "PortfolioQuery";

    /// <summary>The Fund-owned query route.</summary>
    public const string Fund = "PortfolioFundQuery";

    /// <summary>The Financial Policy-owned query route.</summary>
    public const string FinancialPolicy = "PortfolioFinancialPolicyQuery";
}
