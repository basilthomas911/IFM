using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

/// <summary>
/// Typed NATS boundary for consuming operator-facing PostgreSQL sequence identities.
/// Allocation consumes an identity; callers must allow gaps and must never infer allocation from a high watermark.
/// </summary>
public interface IPortfolioIdentityApi
{
    Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateFundIdAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateOrderIdAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateTradeIdAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocatePolicyIdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
