using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Sends durable broker-account commands through the standard command actor boundary.</summary>
public sealed class BrokerAccountCommandApi(IActorProducer producer)
    : NatsClientApi(producer), IBrokerAccountCommandApi
{
    /// <inheritdoc />
    public ValueTask<ServiceResult<Guid>> SubmitQualificationEvidenceAsync(BrokerAccountId accountId,
        string manifestHash, string evidenceReference, DateTime submittedAtUtc,
        CancellationToken cancellationToken = default) => SendAsync(new SubmitAccountQualificationEvidenceCommand
    {
        CommandId = Guid.NewGuid(), EntityId = accountId,
        Subject = Subject(accountId, SubmitAccountQualificationEvidenceCommand.Verb),
        ManifestHash = manifestHash, EvidenceReference = evidenceReference,
        SubmittedAtUtc = submittedAtUtc
    }, accountId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<ServiceResult<Guid>> AcceptQualificationAsync(BrokerAccountId accountId,
        Guid approvalId, string manifestHash, string authorizedBy, DateTime reviewedAtUtc,
        CancellationToken cancellationToken = default) => SendAsync(new AcceptAccountQualificationCommand
    {
        CommandId = Guid.NewGuid(), EntityId = accountId,
        Subject = Subject(accountId, AcceptAccountQualificationCommand.Verb),
        ApprovalId = approvalId, ManifestHash = manifestHash,
        AuthorizedBy = authorizedBy, ReviewedAtUtc = reviewedAtUtc
    }, accountId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<ServiceResult<Guid>> RevokeQualificationAsync(BrokerAccountId accountId,
        string reason, string authorizedBy, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default) => SendAsync(new RevokeAccountQualificationCommand
    {
        CommandId = Guid.NewGuid(), EntityId = accountId,
        Subject = Subject(accountId, RevokeAccountQualificationCommand.Verb),
        Reason = reason, AuthorizedBy = authorizedBy, RevokedAtUtc = revokedAtUtc
    }, accountId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<ServiceResult<Guid>> SetManualHoldAsync(BrokerAccountId accountId,
        string reason, DateTime effectiveAtUtc, CancellationToken cancellationToken = default) =>
        SendAsync(new SetManualTradingHoldCommand
        {
            CommandId = Guid.NewGuid(), EntityId = accountId,
            Subject = Subject(accountId, SetManualTradingHoldCommand.Verb),
            Reason = reason, EffectiveAtUtc = effectiveAtUtc
        }, accountId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<ServiceResult<Guid>> ReleaseManualHoldAsync(BrokerAccountId accountId,
        string reason, DateTime effectiveAtUtc, CancellationToken cancellationToken = default) =>
        SendAsync(new ReleaseManualTradingHoldCommand
        {
            CommandId = Guid.NewGuid(), EntityId = accountId,
            Subject = Subject(accountId, ReleaseManualTradingHoldCommand.Verb),
            Reason = reason, EffectiveAtUtc = effectiveAtUtc
        }, accountId, cancellationToken);

    /// <inheritdoc />
    public ValueTask<ServiceResult<Guid>> RequestResynchronizationAsync(BrokerAccountId accountId,
        DateTime requestedAtUtc, CancellationToken cancellationToken = default) =>
        SendAsync(new RequestBrokerAccountResynchronizationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = accountId,
            Subject = Subject(accountId, RequestBrokerAccountResynchronizationCommand.Verb),
            RequestedAtUtc = requestedAtUtc
        }, accountId, cancellationToken);

    private ValueTask<ServiceResult<Guid>> SendAsync<TCommand>(TCommand command,
        BrokerAccountId accountId, CancellationToken cancellationToken)
        where TCommand : BrokerAccountCommand =>
        RequestCommandAsync<TCommand, BrokerAccountId>(command, accountId, cancellationToken);

    private static ActorSubject Subject(BrokerAccountId accountId, string verb) =>
        new(ActorType.Command, BrokerAccountActorNames.Command, verb, accountId.Format());
}
