using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Revokes an accepted qualification and closes new-risk immediately.</summary>
public static class RevokeAccountQualification
{
    /// <summary>Records a material revocation once.</summary>
    public static ServiceResult<GuidResult> Execute(this RevokeAccountQualificationCommand command,
        BrokerAccountCommandState state)
    {
        var current = state.Current;
        if (current is null) return command.UpdateFailed("BA.NOT_FOUND");
        if (current.QualificationStatus == BrokerAccountQualificationStatus.Revoked &&
            current.Reason == command.Reason)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        var next = current with
        {
            QualificationStatus = BrokerAccountQualificationStatus.Revoked,
            Gate = BrokerAccountOperationalGate.Closed,
            AuthorizedBy = command.AuthorizedBy,
            Reason = command.Reason,
            ChangedAtUtc = command.RevokedAtUtc,
            Revision = current.Revision + 1
        };
        return BrokerAccountCommandResult.Apply(command, state, next);
    }
}
