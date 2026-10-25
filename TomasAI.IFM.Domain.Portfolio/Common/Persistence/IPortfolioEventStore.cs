using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

public interface IPortfolioEventStore : TomasAI.IFM.Application.Contracts.Portfolio.IPortfolioFundOrderReader
{
    async Task<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel> TomasAI.IFM.Application.Contracts.Portfolio.IPortfolioFundOrderReader.ReadOrderAsync(PortfolioFundId fund, int orderId, CancellationToken cancellationToken)
        => (await LoadFundAsync(fund, cancellationToken).ConfigureAwait(false)).Composition(orderId).Order;
    /// <summary>Atomically commits an accepted policy activation and its Portfolio assignment.</summary>
    Task AppendPolicyAndPortfolioAsync(PortfolioFinancialPolicyId policyId,IPortfolioFinancialPolicyDomainEvent policyEvent,
        PortfolioId portfolioId,IPortfolioDomainEvent portfolioEvent,CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Compound policy writes require a transactional Portfolio event store.");
    Task AppendPortfolioAsync(PortfolioId portfolioId, IPortfolioDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default);
    Task AppendFundAsync(PortfolioFundId fundId, IPortfolioFundDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default);
    Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default);
    Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default);
    Task SavePortfolioSnapshotAsync(PortfolioId portfolioId, PortfolioAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default);
    Task SaveFundSnapshotAsync(PortfolioFundId fundId, PortfolioFundAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default);
    Task<IPortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId portfolioId, Guid commandId, CancellationToken cancellationToken = default);
    Task<IPortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId fundId, Guid commandId, CancellationToken cancellationToken = default);
    Task<PortfolioCreatedEvent?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId portfolioId, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<FundMandateCreatedEvent?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId fundId, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IPortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId portfolioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IPortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default);
    Task AppendPolicyAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<PortfolioFinancialPolicyAggregate> LoadPolicyAsync(PortfolioFinancialPolicyId policyId, CancellationToken cancellationToken = default) => Task.FromResult(new PortfolioFinancialPolicyAggregate());
    Task<IReadOnlyList<IPortfolioFinancialPolicyDomainEvent>> LoadPolicyHistoryAsync(PortfolioFinancialPolicyId policyId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IPortfolioFinancialPolicyDomainEvent>>([]);
    Task<IPortfolioFinancialPolicyDomainEvent?> FindCommittedPolicyCommandAsync(PortfolioFinancialPolicyId policyId, Guid commandId, CancellationToken cancellationToken = default) => Task.FromResult<IPortfolioFinancialPolicyDomainEvent?>(null);
}
