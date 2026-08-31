using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioPage<T>
{
    [Key(0)] public T[] Items { get; init; } = [];
    [Key(1)] public string? NextPageToken { get; init; }
    [Key(2)] public int PageSize { get; init; }
}

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundStrategyReferenceCombination
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public long PortfolioVersion { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public long FundMandateVersion { get; init; }
    [Key(4)] public int TradingYear { get; init; }
    [Key(5)] public string DecisionHorizon { get; init; } = string.Empty;
    [Key(6)] public string UnderlyingRoot { get; init; } = string.Empty;
    [Key(7)] public string AssetType { get; init; } = string.Empty;
    [Key(8)] public string TradeFamily { get; init; } = string.Empty;
    [Key(9)] public Guid TradeTemplateId { get; init; }
    [Key(10)] public long TradeTemplateVersion { get; init; }
    [Key(11)] public Guid TradeSelectionHintProfileId { get; init; }
    [Key(12)] public long TradeSelectionHintProfileVersion { get; init; }
    [Key(13)] public Guid OrderCompositionProfileId { get; init; }
    [Key(14)] public long OrderCompositionProfileVersion { get; init; }
    [Key(15)] public bool CurrentlyEligible { get; init; }
    [Key(16)] public string ReasonCode { get; init; } = string.Empty;
}

public enum PortfolioBusinessIdentityKind
{
    Unknown = 0,
    Portfolio = 1,
    Fund = 2,
    Order = 3,
    Trade = 4,
    Policy = 5,
}

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioBusinessIdAllocation
{
    [Key(0)] public PortfolioBusinessIdentityKind Kind { get; init; }
    [Key(1)] public int Value { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
}

[MessagePackObject]
public sealed record PortfolioAggregateRevision
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int? FundId { get; init; }
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public long SourceEventId { get; init; }
}

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

public interface IPortfolioFundCommandApi
{
    Task<ServiceResult<Guid>> CreateFundMandateAsync(FundMandateReadModel mandate, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AddFundMandateVersionAsync(FundMandateReadModel mandate, long expectedVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> ChangeFundStateAsync(PortfolioFundId fundId, long expectedVersion, FundOperatingState state, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AssignTradeTemplateAsync(FundTradeTemplateAssignmentReadModel assignment, long expectedVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundCompositionReservationResult>> ReserveCompositionAsync(ReserveFundOrderCompositionRequest request, PortfolioFundStrategySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundCompositionReservationResult>> CreateManualOrderAsync(CreateManualFundOrderRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> MarkComposingAsync(PortfolioFundOrderId orderId, long expectedVersion, Guid invocationId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> RecordComposedAsync(PortfolioFundOrderId orderId, long expectedVersion, OrderCompositionResultReference result, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> RecordRiskOutcomeAsync(PortfolioFundOrderId orderId, long expectedVersion, RiskManagementResultReference result, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> CancelCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> ExpireCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default);
}

public interface IPortfolioFinancialPolicyCommandApi
{
    Task<ServiceResult<Guid>> CreatePolicyAsync(PortfolioFinancialPolicyReadModel policy, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AddPolicyVersionAsync(PortfolioFinancialPolicyReadModel policy, long expectedRevision, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> ActivateAndAssignAsync(PortfolioFinancialPolicyId policyId, long policyVersion, long expectedPolicyRevision, long expectedPortfolioRevision, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> RetirePolicyAsync(PortfolioFinancialPolicyId policyId, long policyVersion, long expectedRevision, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> DeleteDraftPolicyAsync(PortfolioFinancialPolicyId policyId, long expectedRevision, string reason, CancellationToken cancellationToken = default);
}

public interface IPortfolioQueryApi
{
    Task<ServiceResult<PortfolioReadModel>> GetPortfolioAsync(int portfolioId, long? version = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioAggregateRevision>> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<PortfolioReadModel>>> GetPortfoliosAsync(PortfolioOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundMandateReadModel>> GetFundAsync(int portfolioId, int fundId, long? version = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioAggregateRevision>> GetFundRevisionAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<FundMandateReadModel>>> GetFundsAsync(int portfolioId, FundOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundAllocationReadModel>> GetFundAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundRiskEnvelopeReadModel>> GetFundRiskEnvelopeAsync(int portfolioId, int fundId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundTradeTemplateAssignmentReadModel[]>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioFundStrategySnapshot>> GetStrategySnapshotAsync(int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderTradeProjectionReadModel>> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundCompositionWorkflowProjectionReadModel[]>> GetCompositionByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>> GetOrderTradesAsync(int orderId, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> GetStrategyReferenceCombinationsAsync(int portfolioId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetPolicyAsync(int policyId, long? policyVersion = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<PortfolioPage<PortfolioFinancialPolicyReadModel>>> GetPoliciesAsync(int portfolioId, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetActivePolicyAsync(int portfolioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<LegacyPortfolioScopeReadModel[]>> GetLegacyPortfolioScopesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<LegacyFundHistoryReadModel[]>> GetLegacyFundCatalogAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<LegacyFundOrderHistoryReadModel[]>> GetLegacyFundOrdersAsync(int legacyFundId, DateOnly fromDate, DateOnly toDate, int pageSize = 1000, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<LegacyFundTradeHistoryReadModel[]>> GetLegacyFundOrderTradesAsync(int legacyFundId, int orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
