using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class PortfolioIdentityApi(IActorProducer actorProducer) : NatsClientApi(actorProducer), IPortfolioIdentityApi
{
    public Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default) =>
        AllocateAsync(PortfolioBusinessIdentityKind.Portfolio, cancellationToken);

    public Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateFundIdAsync(CancellationToken cancellationToken = default) =>
        AllocateAsync(PortfolioBusinessIdentityKind.Fund, cancellationToken);

    public Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateOrderIdAsync(CancellationToken cancellationToken = default) =>
        AllocateAsync(PortfolioBusinessIdentityKind.Order, cancellationToken);

    public Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateTradeIdAsync(CancellationToken cancellationToken = default) =>
        AllocateAsync(PortfolioBusinessIdentityKind.Trade, cancellationToken);

    public Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocatePolicyIdAsync(CancellationToken cancellationToken = default) =>
        AllocateAsync(PortfolioBusinessIdentityKind.Policy, cancellationToken);

    Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateAsync(PortfolioBusinessIdentityKind kind, CancellationToken cancellationToken) =>
        AllocateForOperationAsync(kind, Guid.NewGuid(), cancellationToken);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Typed client transport boundary converts failures to the declared ServiceResult via OnError rather than throwing across the API contract.")]
    public async Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateForOperationAsync(
        PortfolioBusinessIdentityKind kind,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var request = AllocatePortfolioBusinessIdCommand.ForOperation(operationId) with
        {
            Kind = kind,
            Access = PortfolioAccessScope.Current ?? PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}"),
        };
        try
        {
            var result = await RequestFunctionAsync<AllocatePortfolioBusinessIdCommand, ActorEntityId,
                FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>>(
                    request, request.EntityId, cancellationToken).ConfigureAwait(false);
            if (result is { Success: true, Value: { IsCompleted: true, Completed: { } allocation } }
                && allocation.CommandId == operationId && allocation.Value is { Value: > 0 } && allocation.Value.Kind == kind)
                return new ServiceOk<PortfolioBusinessIdAllocation>(allocation.Value with
                {
                    CorrelationId = correlationId, OperationId = operationId
                });
            return new ServiceFailed<PortfolioBusinessIdAllocation>(
                result.Value?.Failed?.ErrorCode ?? (result.ErrorCode != 0 ? result.ErrorCode : PortfolioErrorCodes.SequenceAllocationFailed),
                result.Value?.Failed?.ErrorMessage ?? result.ErrorMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ServiceFailed<PortfolioBusinessIdAllocation>(PortfolioErrorCodes.SequenceAllocationFailed, exception.Message);
        }
    }
}
