using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

public interface IPortfolioCommandApi
{
    Task<ServiceResult<Guid>> CreatePortfolioAsync(PortfolioReadModel portfolio, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AddPortfolioVersionAsync(PortfolioReadModel portfolio, long expectedVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> ChangePortfolioStateAsync(PortfolioId portfolioId, long expectedVersion, PortfolioOperatingState state, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AddFundAsync(PortfolioFundId fundId, long expectedPortfolioVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> DelegateAllocationAsync(FundAllocationReadModel allocation, long expectedPortfolioVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> DelegateRiskEnvelopeAsync(FundRiskEnvelopeReadModel envelope, long expectedPortfolioVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> RetirePortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> DeleteDraftPortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default);
}
