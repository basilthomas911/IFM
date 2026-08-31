using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
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

    async Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateAsync(
        PortfolioBusinessIdentityKind kind,
        CancellationToken cancellationToken)
    {
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var subject = new ActorSubject(ActorType.Query, PortfolioQuerySubjects.Actor, "AllocatePortfolioBusinessId", kind.ToString());
        var query = new PortfolioQuery<AllocatePortfolioBusinessIdRequest, PortfolioBusinessIdAllocation>
        {
            Subject = subject,
            Parameters = new(kind),
            CorrelationId = correlationId,
            RequestedOnUtc = DateTime.UtcNow,
        };
        try
        {
            return await RequestAsync<PortfolioQuery<AllocatePortfolioBusinessIdRequest, PortfolioBusinessIdAllocation>, PortfolioBusinessIdAllocation>(
                subject, query, cancellationToken).ConfigureAwait(false);
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
