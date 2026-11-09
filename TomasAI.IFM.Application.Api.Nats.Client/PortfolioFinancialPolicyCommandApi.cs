using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class PortfolioFinancialPolicyCommandApi(IActorProducer actorProducer, IPortfolioQueryApi? queries = null)
    : NatsClientApi(actorProducer), IPortfolioFinancialPolicyCommandApi
{
    static PortfolioAccessContext Access => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}");
    public Task<ServiceResult<Guid>> CreatePolicyAsync(PortfolioFinancialPolicyReadModel policy, Guid idempotencyKey, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(policy);
        return Send(new(policy.PortfolioId, policy.PolicyId), CreatePortfolioFinancialPolicyCommand.Verb,
            new CreatePortfolioFinancialPolicyCommand(policy, idempotencyKey), cancellationToken,
            IdempotentCommandId.Create(idempotencyKey, policy));
    }
    public Task<ServiceResult<Guid>> AddPolicyVersionAsync(PortfolioFinancialPolicyReadModel policy, long expectedRevision, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(policy);
        return Send(new(policy.PortfolioId, policy.PolicyId), AddPortfolioFinancialPolicyVersionCommand.Verb, new AddPortfolioFinancialPolicyVersionCommand(policy, expectedRevision), cancellationToken);
    }
    public Task<ServiceResult<Guid>> ActivateAndAssignAsync(PortfolioFinancialPolicyId policyId, long policyVersion, long expectedPolicyRevision, long expectedPortfolioRevision, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(policyId);
        return Send(policyId, ActivateAndAssignPortfolioFinancialPolicyCommand.Verb, new ActivateAndAssignPortfolioFinancialPolicyCommand(policyVersion, expectedPolicyRevision, expectedPortfolioRevision), cancellationToken);
    }
    public Task<ServiceResult<Guid>> RetirePolicyAsync(PortfolioFinancialPolicyId policyId, long policyVersion, long expectedRevision, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(policyId);
        return Send(policyId, RetirePortfolioFinancialPolicyCommand.Verb, new RetirePortfolioFinancialPolicyCommand(policyVersion, expectedRevision, reason), cancellationToken);
    }
    public Task<ServiceResult<Guid>> DeleteDraftPolicyAsync(PortfolioFinancialPolicyId policyId, long expectedRevision, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(policyId);
        return Send(policyId, DeleteDraftPortfolioFinancialPolicyCommand.Verb, new DeleteDraftPortfolioFinancialPolicyCommand(expectedRevision, reason), cancellationToken);
    }

    async Task<ServiceResult<Guid>> Send<T>(PortfolioFinancialPolicyId id, string verb, T payload, CancellationToken cancellationToken, Guid? operationId = null)
    {
        var subject = new ActorSubject(ActorType.Command, CreatePortfolioFinancialPolicyCommand.Actor, verb, id.Format());
        var commandId = operationId ?? Guid.NewGuid();
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var requestedOnUtc = DateTime.UtcNow;
        var access = Access;
        object command = payload switch
        {
            CreatePortfolioFinancialPolicyCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access }),
            AddPortfolioFinancialPolicyVersionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access }),
            ActivateAndAssignPortfolioFinancialPolicyCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access }),
            RetirePortfolioFinancialPolicyCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access }),
            DeleteDraftPortfolioFinancialPolicyCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = commandId, Subject = subject, EntityId = id, ErrorCode = PortfolioErrorCodes.ValidationFailed, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access }),
            _ => throw new InvalidOperationException($"Unsupported Portfolio FinancialPolicy command payload {typeof(T).FullName}."),
        };
        try
        {
            if (command is CreatePortfolioFinancialPolicyCommand or AddPortfolioFinancialPolicyVersionCommand or ActivateAndAssignPortfolioFinancialPolicyCommand)
            {
                var policy = command switch
                {
                    CreatePortfolioFinancialPolicyCommand value => value.Policy,
                    AddPortfolioFinancialPolicyVersionCommand value => value.Policy,
                    _ => null
                };
                if (command is ActivateAndAssignPortfolioFinancialPolicyCommand activate)
                {
                    if (queries is null)
                        return new ServiceFailed<Guid>(PortfolioErrorCodes.Unavailable, "Policy activation requires the Portfolio query API.");
                    var projected = await queries.GetPolicyAsync(id.PolicyId, activate.PolicyVersion, cancellationToken).ConfigureAwait(false);
                    if (!projected.Success || projected.Value is not { } exactPolicy || exactPolicy.PortfolioId != id.PortfolioId)
                        return new ServiceFailed<Guid>(PortfolioErrorCodes.ValidationFailed, "The exact Portfolio Financial Policy version is unavailable for activation.");
                    policy = exactPolicy;
                    var before = await queries.GetPortfolioRevisionAsync(id.PortfolioId, cancellationToken).ConfigureAwait(false);
                    var parent = await queries.GetPortfolioAsync(id.PortfolioId, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var after = await queries.GetPortfolioRevisionAsync(id.PortfolioId, cancellationToken).ConfigureAwait(false);
                    if (!before.Success || !after.Success || before.Value is not { } revision
                        || revision.Revision != activate.ExpectedPortfolioRevision || revision != after.Value
                        || !parent.Success || parent.Value is not { } exactParent || exactParent.PortfolioId != id.PortfolioId)
                        return new ServiceFailed<Guid>(PortfolioErrorCodes.VersionConflict, "The projected parent Portfolio does not match the expected revision.");
                    command = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(activate, activate with { ParentPortfolio = exactParent });
                }
                if (policy is not null)
                {
                    var preparation = ValidatePortfolioReferenceCommand.ForOwnerCommand(commandId) with
                    {
                        Access = access,
                        Policy = policy,
                        ActivatesPolicy = command is ActivateAndAssignPortfolioFinancialPolicyCommand
                    };
                    var receipt = await ValidatePortfolioReferenceAsync(preparation, cancellationToken).ConfigureAwait(false);
                    if (!receipt.Success) return receipt;
                    command = command switch
                    {
                        CreatePortfolioFinancialPolicyCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with { ReferencePreparationId = receipt.Value }),
                        AddPortfolioFinancialPolicyVersionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with { ReferencePreparationId = receipt.Value }),
                        ActivateAndAssignPortfolioFinancialPolicyCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with { ReferencePreparationId = receipt.Value }),
                        _ => command
                    };
                }
            }
            return await RequestCommandAsync((dynamic)command, id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(PortfolioErrorCodes.Unavailable, ex.Message); }
    }
}
