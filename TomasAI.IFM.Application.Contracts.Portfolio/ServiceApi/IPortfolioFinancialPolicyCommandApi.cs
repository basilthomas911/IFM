using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

public interface IPortfolioFinancialPolicyCommandApi
{
    Task<ServiceResult<Guid>> CreatePolicyAsync(PortfolioFinancialPolicyReadModel policy, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AddPolicyVersionAsync(PortfolioFinancialPolicyReadModel policy, long expectedRevision, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> ActivateAndAssignAsync(PortfolioFinancialPolicyId policyId, long policyVersion, long expectedPolicyRevision, long expectedPortfolioRevision, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> RetirePolicyAsync(PortfolioFinancialPolicyId policyId, long policyVersion, long expectedRevision, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> DeleteDraftPolicyAsync(PortfolioFinancialPolicyId policyId, long expectedRevision, string reason, CancellationToken cancellationToken = default);
}
