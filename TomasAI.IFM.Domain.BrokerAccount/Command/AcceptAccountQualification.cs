using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Records the explicit human acceptance of reviewed emulator evidence.</summary>
public static class AcceptAccountQualification
{
    /// <summary>Accepts only the exact currently reviewed manifest.</summary>
    public static ServiceResult<GuidResult> Execute(this AcceptAccountQualificationCommand command,
        BrokerAccountCommandState state)
    {
        var current = state.Current;
        if (current is null || current.QualificationStatus != BrokerAccountQualificationStatus.ReviewPending)
            return command.UpdateFailed("BA.QUALIFICATION.NOT_REVIEW_PENDING");
        if (!string.Equals(current.ManifestHash, command.ManifestHash, StringComparison.Ordinal))
            return command.UpdateFailed("BA.QUALIFICATION.MANIFEST_CONFLICT");
        var next = current with
        {
            QualificationStatus = BrokerAccountQualificationStatus.Accepted,
            ApprovalId = command.ApprovalId,
            AuthorizedBy = command.AuthorizedBy,
            ChangedAtUtc = command.ReviewedAtUtc,
            Revision = current.Revision + 1
        };
        next = next with { Gate = BrokerAccountCommandResult.Gate(next) };
        return BrokerAccountCommandResult.Apply(command, state, next);
    }
}
