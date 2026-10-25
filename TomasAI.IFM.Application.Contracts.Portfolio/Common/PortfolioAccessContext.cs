using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Common;

/// <summary>Identifies the authenticated caller and Portfolio roles presented to an actor.</summary>
[MessagePackObject]
public sealed record PortfolioAccessContext
{
    [Key(0)] public string Principal { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(1)] public string[] Roles { get; init; } = [];

    /// <summary>Creates read-only Portfolio access.</summary>
    /// <param name="principal">The authenticated caller identity.</param>
    /// <returns>The caller access context.</returns>
    public static PortfolioAccessContext Reader(string principal) => new() { Principal = principal, Roles = ["PortfolioReader"] };

    /// <summary>Creates Portfolio administrator access.</summary>
    /// <param name="principal">The authenticated caller identity.</param>
    /// <returns>The caller access context.</returns>
    public static PortfolioAccessContext Administrator(string principal) => new() { Principal = principal, Roles = ["PortfolioAdministrator"] };

    /// <summary>Creates strategy-workflow Portfolio access.</summary>
    /// <param name="principal">The authenticated caller identity.</param>
    /// <returns>The caller access context.</returns>
    public static PortfolioAccessContext Workflow(string principal) => new() { Principal = principal, Roles = ["StrategyWorkflow"] };
}
