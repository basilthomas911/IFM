using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public sealed record PortfolioProjection<T>(T Value, int SchemaVersion, long AggregateVersion, long SourceEventId, DateTime UpdatedOnUtc, string PayloadHash)
{
    public static PortfolioProjection<T> Create(T value, long aggregateVersion, long sourceEventId, DateTime updatedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (aggregateVersion <= 0 || sourceEventId <= 0) throw new ArgumentOutOfRangeException(nameof(aggregateVersion));
        if (updatedOnUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Projection timestamp must be UTC.", nameof(updatedOnUtc));
        var payload = System.Text.Json.JsonSerializer.Serialize(value);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return new(value, 1, aggregateVersion, sourceEventId, updatedOnUtc, hash);
    }
}

public sealed record PortfolioProjectionRevision(int PortfolioId, int? FundId, long AggregateRevision, long SourceEventId);
public sealed record DraftFundProjectionDeletion(int FundId, long[] MandateVersions);
public sealed record DraftPortfolioProjectionDeletion(int PortfolioId, int StateBucket, DraftFundProjectionDeletion[] Funds, long SourceEventId);
public sealed record DraftPolicyProjectionDeletion(int PortfolioId, int PolicyId, long SourceEventId);

public interface IPortfolioDbReadContext
{
    Task<PortfolioReadModel?> GetPortfolioAsync(int portfolioId, CancellationToken cancellationToken = default);
    Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState state, int bucket, int afterPortfolioId, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int portfolioId, int afterFundId, int pageSize, CancellationToken cancellationToken = default);
    Task<FundMandateReadModel?> GetFundAsync(int fundId, CancellationToken cancellationToken = default);
    Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int fundId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int portfolioId, int tradingYear, string decisionHorizon, DateTime effectiveAtUtc, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, int pageSize, CancellationToken cancellationToken = default);
    Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default);
    Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default);
    Task<FundOrderProjectionReadModel?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int orderId, int pageSize, CancellationToken cancellationToken = default);
    Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid workflowId, int pageSize, CancellationToken cancellationToken = default);
    Task<PortfolioFinancialPolicyReadModel?> GetPolicyAsync(int policyId, long? policyVersion = null, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioFinancialPolicyReadModel?>(null);
    Task<IReadOnlyList<PortfolioFinancialPolicyReadModel>> GetPoliciesAsync(int portfolioId, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortfolioFinancialPolicyReadModel>>([]);
    Task<PortfolioFinancialPolicyReadModel?> GetActivePolicyAsync(int portfolioId, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioFinancialPolicyReadModel?>(null);
}

public interface IPortfolioDbWriteContext
{
    Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int stateBucket, CancellationToken cancellationToken = default);
    Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken cancellationToken = default);
    Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row, CancellationToken cancellationToken = default);
    Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row, CancellationToken cancellationToken = default);
    Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row, CancellationToken cancellationToken = default);
    Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row, DateOnly orderMonth, CancellationToken cancellationToken = default);
    Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row, CancellationToken cancellationToken = default);
    Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row, CancellationToken cancellationToken = default);
    Task DeleteDraftPortfolioAsync(DraftPortfolioProjectionDeletion deletion, CancellationToken cancellationToken = default);
    Task UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel> row, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task DeleteDraftPolicyAsync(DraftPolicyProjectionDeletion deletion, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public interface IPortfolioDbContext : IPortfolioDbReadContext, IPortfolioDbWriteContext, TomasAI.IFM.Framework.Storage.IObjectRepository<PortfolioDbContext>;
