using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class PortfolioCommandApi(IActorProducer actorProducer) : NatsClientApi(actorProducer), IPortfolioCommandApi
{
    static PortfolioAccessContext Access => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}");
    public Task<ServiceResult<Guid>> CreatePortfolioAsync(PortfolioReadModel portfolio, Guid idempotencyKey, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        return Send(new PortfolioId(portfolio.PortfolioId), CreatePortfolioCommand.Verb, new CreatePortfolioCommand(portfolio, idempotencyKey), PortfolioErrorCodes.ValidationFailed, cancellationToken, IdempotentCommandId.Create(idempotencyKey, portfolio));
    }
    public Task<ServiceResult<Guid>> AddPortfolioVersionAsync(PortfolioReadModel portfolio, long expectedVersion, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        return Send(new PortfolioId(portfolio.PortfolioId), AddPortfolioVersionCommand.Verb, new AddPortfolioVersionCommand(portfolio, expectedVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    }
    public Task<ServiceResult<Guid>> ChangePortfolioStateAsync(PortfolioId portfolioId, long expectedVersion, PortfolioOperatingState state, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        return Send(portfolioId, ChangePortfolioOperatingStateCommand.Verb, new ChangePortfolioOperatingStateCommand(expectedVersion, state, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    }
    public Task<ServiceResult<Guid>> AddFundAsync(PortfolioFundId fundId, long expectedPortfolioVersion, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(fundId);
        return Send(new PortfolioId(fundId.PortfolioId), AddFundToPortfolioCommand.Verb, new AddFundToPortfolioCommand(fundId, expectedPortfolioVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    }
    public Task<ServiceResult<Guid>> DelegateAllocationAsync(FundAllocationReadModel allocation, long expectedPortfolioVersion, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(allocation);
        return Send(new PortfolioId(allocation.PortfolioId), DelegateFundAllocationCommand.Verb, new DelegateFundAllocationCommand(allocation, expectedPortfolioVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);
    }
    public Task<ServiceResult<Guid>> DelegateRiskEnvelopeAsync(FundRiskEnvelopeReadModel envelope, long expectedPortfolioVersion, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Send(new PortfolioId(envelope.PortfolioId), DelegateFundRiskEnvelopeCommand.Verb, new DelegateFundRiskEnvelopeCommand(envelope, expectedPortfolioVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);
    }
    public Task<ServiceResult<Guid>> RetirePortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        return Send(portfolioId, RetirePortfolioCommand.Verb, new RetirePortfolioCommand(expectedVersion, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    }
    public Task<ServiceResult<Guid>> DeleteDraftPortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        return Send(portfolioId, DeleteDraftPortfolioCommand.Verb, new DeleteDraftPortfolioCommand(expectedVersion, reason), PortfolioErrorCodes.DraftDeletionNotAllowed, cancellationToken);
    }

    async Task<ServiceResult<Guid>> Send<TCommand>(PortfolioId id, string verb, TCommand message, int errorCode, CancellationToken cancellationToken, Guid? commandId = null)
    {
        var subject = new ActorSubject(ActorType.Command, CreatePortfolioCommand.Actor, verb, id.Format());
        var actualCommandId = commandId ?? Guid.NewGuid();
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var requestedOnUtc = DateTime.UtcNow;
        object command = message switch
        {
            CreatePortfolioCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            AddPortfolioVersionCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            ChangePortfolioOperatingStateCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            AddFundToPortfolioCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            DelegateFundAllocationCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            DelegateFundRiskEnvelopeCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            RetirePortfolioCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            DeleteDraftPortfolioCommand value => value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = Access },
            _ => throw new InvalidOperationException($"Unsupported Portfolio command message {typeof(TCommand).FullName}."),
        };
        try { return await RequestCommandAsync((dynamic)command, id, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(errorCode, ex.Message); }
    }
}
