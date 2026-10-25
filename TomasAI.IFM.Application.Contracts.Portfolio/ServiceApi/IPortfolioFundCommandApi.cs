using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

public interface IPortfolioFundCommandApi
{
    Task<ServiceResult<Guid>> CreateFundMandateAsync(FundMandateReadModel mandate, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AddFundMandateVersionAsync(FundMandateReadModel mandate, long expectedVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> ChangeFundStateAsync(PortfolioFundId fundId, long expectedVersion, FundOperatingState state, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> AssignTradeTemplateAsync(FundTradeTemplateAssignmentReadModel assignment, long expectedVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundCompositionReservationResult>> ReserveCompositionAsync(ReserveFundOrderCompositionRequest request, PortfolioFundStrategySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundCompositionReservationResult>> CreateManualOrderAsync(CreateManualFundOrderRequest request, CancellationToken cancellationToken = default);
    /// <summary>Adds one operator-authored trade to an existing manual Portfolio Fund order.</summary>
    Task<ServiceResult<FundCompositionReservationResult>> AddManualTradeAsync(
        AddManualFundOrderTradeRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> MarkComposingAsync(PortfolioFundOrderId orderId, long expectedVersion, Guid invocationId, CancellationToken cancellationToken = default);
    /// <summary>Removes an economically inactive trade from a manual Portfolio Fund order.</summary>
    Task<ServiceResult<FundCompositionReservationResult>> RemoveManualTradeAsync(
        ManualFundOrderTradeMutationRequest request, CancellationToken cancellationToken = default);
    /// <summary>Deletes an empty draft manual Portfolio Fund order.</summary>
    /// <param name="request">The scoped order-deletion request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The accepted deletion command identifier.</returns>
    Task<ServiceResult<Guid>> DeleteManualOrderAsync(
        ManualFundOrderMutationRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> RecordComposedAsync(PortfolioFundOrderId orderId, long expectedVersion, OrderCompositionResultReference result, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> AuthorizeRiskAsync(Guid commandId, PortfolioFundOrderId orderId, long expectedVersion,
        Financial.FundRiskAuthorizationReference authorization, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Financial Fund authorization is unavailable.");
    Task<ServiceResult<FundOrderProjectionReadModel>> RecordRiskOutcomeAsync(PortfolioFundOrderId orderId, long expectedVersion, RiskManagementResultReference result, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> CancelCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> ExpireCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default);
}
