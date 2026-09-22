using TomasAI.IFM.Domain.Portfolio.Shared.Common;
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
        Send(new(policy.PortfolioId, policy.PolicyId), CreatePortfolioFinancialPolicyCommand.Verb, new CreatePortfolioFinancialPolicyCommand(policy, idempotencyKey), cancellationToken);
    public Task<ServiceResult<Guid>> AddPolicyVersionAsync(PortfolioFinancialPolicyReadModel policy, long expectedRevision, CancellationToken cancellationToken = default) =>
        Send(new(policy.PortfolioId, policy.PolicyId), AddPortfolioFinancialPolicyVersionCommand.Verb, new AddPortfolioFinancialPolicyVersionCommand(policy, expectedRevision), cancellationToken);
    public Task<ServiceResult<Guid>> ActivateAndAssignAsync(PortfolioFinancialPolicyId id, long version, long expectedPolicyRevision, long expectedPortfolioRevision, CancellationToken cancellationToken = default) =>
        Send(id, ActivateAndAssignPortfolioFinancialPolicyCommand.Verb, new ActivateAndAssignPortfolioFinancialPolicyCommand(version, expectedPolicyRevision, expectedPortfolioRevision), cancellationToken);
    public Task<ServiceResult<Guid>> RetirePolicyAsync(PortfolioFinancialPolicyId id, long version, long expectedRevision, string reason, CancellationToken cancellationToken = default) =>
        Send(id, RetirePortfolioFinancialPolicyCommand.Verb, new RetirePortfolioFinancialPolicyCommand(version, expectedRevision, reason), cancellationToken);
    public Task<ServiceResult<Guid>> DeleteDraftPolicyAsync(PortfolioFinancialPolicyId id, long expectedRevision, string reason, CancellationToken cancellationToken = default) =>
        Send(id, DeleteDraftPortfolioFinancialPolicyCommand.Verb, new DeleteDraftPortfolioFinancialPolicyCommand(expectedRevision, reason), cancellationToken);

    async Task<ServiceResult<Guid>> Send<T>(PortfolioFinancialPolicyId id, string verb, T payload, CancellationToken cancellationToken)
    {
        var subject = new ActorSubject(ActorType.Command, CreatePortfolioFinancialPolicyCommand.Actor, verb, id.Format());
        var commandId = Guid.NewGuid();
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var requestedOnUtc = DateTime.UtcNow;
        object command = payload switch
        {
            CreatePortfolioFinancialPolicyCommand value => value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            AddPortfolioFinancialPolicyVersionCommand value => value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            ActivateAndAssignPortfolioFinancialPolicyCommand value => value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            RetirePortfolioFinancialPolicyCommand value => value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            DeleteDraftPortfolioFinancialPolicyCommand value => value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            _ => throw new InvalidOperationException($"Unsupported Portfolio FinancialPolicy command payload {typeof(T).FullName}."),
        };
        try { return await RequestCommandAsync((dynamic)command, id, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(PortfolioErrorCodes.Unavailable, ex.Message); }
    }
}
