

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>One accepted source transition inside a compound Portfolio transaction.</summary>
/// <param name="FundId">Optional owning Fund.</param>
/// <param name="Stream">The source stream identity.</param>
/// <param name="Source">Complete accepted source facts.</param>
/// <param name="ExpectedRevision">Expected committed stream revision.</param>
/// <param name="ChangesAuthority">Whether financial admission authority advances.</param>
public sealed record PortfolioAuthorityAppend(int? FundId,string Stream,IEvent Source,long ExpectedRevision,bool ChangesAuthority)
{
    /// <summary>Optional full checkpoint for the accepted source revision.</summary>
    public IEvent? Checkpoint { get; init; }

    /// <summary>Optional upstream Portfolio revision pinned by the Fund command producer.</summary>
    public long? ExpectedPortfolioRevision { get; init; }
    public long? ExpectedPortfolioVersion { get; init; }
    public PortfolioOperatingState? ExpectedPortfolioOperatingState { get; init; }
    public PortfolioPreparedIdentifiers? PreparedIdentifiers { get; init; }
    public int? RequiredUnreferencedPolicyId { get; init; }
    public IReadOnlyList<int>? DraftPortfolioFundIds { get; init; }
}
