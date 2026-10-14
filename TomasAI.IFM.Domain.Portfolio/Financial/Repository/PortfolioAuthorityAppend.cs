

using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>One accepted source transition inside a compound Portfolio transaction.</summary>
/// <param name="FundId">Optional owning Fund.</param>
/// <param name="Stream">The source stream identity.</param>
/// <param name="Source">Complete accepted source facts.</param>
/// <param name="ExpectedRevision">Expected committed stream revision.</param>
/// <param name="ChangesAuthority">Whether financial admission authority advances.</param>
public sealed record PortfolioAuthorityAppend(int? FundId,string Stream,IEvent Source,long ExpectedRevision,bool ChangesAuthority);
