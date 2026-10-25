using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Mutates durable broker-account evidence, qualification, holds, and synchronization state.</summary>
public interface IBrokerAccountCommandApi
{
    /// <summary>Submits a version-bound qualification manifest for human review.</summary>
    ValueTask<ServiceResult<Guid>> SubmitQualificationEvidenceAsync(BrokerAccountId accountId,
        string manifestHash, string evidenceReference, DateTime submittedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Records an explicit human acceptance of the exact submitted manifest.</summary>
    ValueTask<ServiceResult<Guid>> AcceptQualificationAsync(BrokerAccountId accountId,
        Guid approvalId, string manifestHash, string authorizedBy, DateTime reviewedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes the current account qualification and closes new-risk trading.</summary>
    ValueTask<ServiceResult<Guid>> RevokeQualificationAsync(BrokerAccountId accountId,
        string reason, string authorizedBy, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Places an operator hold on new-risk trading.</summary>
    ValueTask<ServiceResult<Guid>> SetManualHoldAsync(BrokerAccountId accountId,
        string reason, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Releases an operator hold and re-evaluates the durable qualification state.</summary>
    ValueTask<ServiceResult<Guid>> ReleaseManualHoldAsync(BrokerAccountId accountId,
        string reason, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Requests an explicit broker-account resynchronization.</summary>
    ValueTask<ServiceResult<Guid>> RequestResynchronizationAsync(BrokerAccountId accountId,
        DateTime requestedAtUtc, CancellationToken cancellationToken = default);
}
