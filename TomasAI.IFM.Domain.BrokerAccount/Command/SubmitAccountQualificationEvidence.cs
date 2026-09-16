using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Submits version-bound qualification evidence for human review.</summary>
public static class SubmitAccountQualificationEvidence
{
    /// <summary>Moves an existing account to review pending without accepting it.</summary>
    public static ServiceResult<GuidResult> Execute(this SubmitAccountQualificationEvidenceCommand command,
        BrokerAccountCommandState state)
    {
        var current = state.Current;
        if (current?.Snapshot is null) return command.UpdateFailed("BA.SNAPSHOT.REQUIRED");
        if (current.ManifestHash == command.ManifestHash &&
            current.QualificationStatus == BrokerAccountQualificationStatus.ReviewPending)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        var next = current with
        {
            ManifestHash = command.ManifestHash,
            EvidenceReference = command.EvidenceReference,
            QualificationStatus = BrokerAccountQualificationStatus.ReviewPending,
            ApprovalId = Guid.Empty,
            AuthorizedBy = string.Empty,
            Gate = BrokerAccountOperationalGate.Closed,
            ChangedAtUtc = command.SubmittedAtUtc,
            Revision = current.Revision + 1
        };
        return BrokerAccountCommandResult.Apply(command, state, next);
    }
}
