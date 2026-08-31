using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class PortfolioFinancialPolicyCommandApi(IActorProducer actorProducer)
    : NatsClientApi(actorProducer), IPortfolioFinancialPolicyCommandApi
{
    static PortfolioAccessContext Access => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}");
    public Task<ServiceResult<Guid>> CreatePolicyAsync(PortfolioFinancialPolicyReadModel policy, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        Send(new(policy.PortfolioId, policy.PolicyId), "CreatePortfolioFinancialPolicy", new CreatePortfolioFinancialPolicyPayload(policy, idempotencyKey), cancellationToken);
    public Task<ServiceResult<Guid>> AddPolicyVersionAsync(PortfolioFinancialPolicyReadModel policy, long expectedRevision, CancellationToken cancellationToken = default) =>
        Send(new(policy.PortfolioId, policy.PolicyId), "AddPortfolioFinancialPolicyVersion", new AddPortfolioFinancialPolicyVersionPayload(policy, expectedRevision), cancellationToken);
    public Task<ServiceResult<Guid>> ActivateAndAssignAsync(PortfolioFinancialPolicyId id, long version, long expectedPolicyRevision, long expectedPortfolioRevision, CancellationToken cancellationToken = default) =>
        Send(id, "ActivateAndAssignPortfolioFinancialPolicy", new ActivateAndAssignPortfolioFinancialPolicyPayload(version, expectedPolicyRevision, expectedPortfolioRevision), cancellationToken);
    public Task<ServiceResult<Guid>> RetirePolicyAsync(PortfolioFinancialPolicyId id, long version, long expectedRevision, string reason, CancellationToken cancellationToken = default) =>
        Send(id, "RetirePortfolioFinancialPolicy", new RetirePortfolioFinancialPolicyPayload(version, expectedRevision, reason), cancellationToken);
    public Task<ServiceResult<Guid>> DeleteDraftPolicyAsync(PortfolioFinancialPolicyId id, long expectedRevision, string reason, CancellationToken cancellationToken = default) =>
        Send(id, "DeleteDraftPortfolioFinancialPolicy", new DeleteDraftPortfolioFinancialPolicyPayload(expectedRevision, reason), cancellationToken);

    async Task<ServiceResult<Guid>> Send<T>(PortfolioFinancialPolicyId id, string verb, T payload, CancellationToken cancellationToken)
    {
        var subject = new ActorSubject(ActorType.Command, PortfolioCommandSubjects.PolicyActor, verb, id.Format());
        var command = new PortfolioCommand<T, PortfolioFinancialPolicyId>
        {
            CommandId = Guid.NewGuid(), Subject = subject, EntityId = id, Payload = payload,
            ErrorCode = PortfolioErrorCodes.ValidationFailed,
            CorrelationId = PortfolioRequestCorrelation.CurrentOrNew(), RequestedOnUtc = DateTime.UtcNow, Access = Access,
        };
        try { return await RequestCommandAsync(command, id, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(PortfolioErrorCodes.Unavailable, ex.Message); }
    }
}
