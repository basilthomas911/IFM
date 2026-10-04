using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Validation;

/// <summary>Accumulates payload and identity errors before state loading.</summary>
public static class BrokerAccountCommandValidation
{
    /// <summary>Checks the command payload and its routing identity without throwing.</summary>
    public static List<ValidationError> ValidateBrokerAccountCommand(this List<ValidationError> errors, ICommand<BrokerAccountId> command)
    {

        if (command is RecordBrokerAccountSnapshotCommand snapshot && (snapshot.Snapshot is null || snapshot.Environment == Application.TradeBroker.Contracts.BrokerEnvironment.Unknown ||
            snapshot.Snapshot.AccountAlias != command.EntityId.AccountAlias ||
            snapshot.Snapshot.Generation <= 0 || snapshot.Snapshot.AsOfUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("A sourced UTC snapshot for the exact account is required."));
        if (command is SubmitAccountQualificationEvidenceCommand evidence && (string.IsNullOrWhiteSpace(evidence.ManifestHash) ||
            string.IsNullOrWhiteSpace(evidence.EvidenceReference) ||
            evidence.SubmittedAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Qualification manifest, evidence reference and UTC submission are required."));
        if (command is AcceptAccountQualificationCommand accept && (accept.ApprovalId == Guid.Empty ||
            string.IsNullOrWhiteSpace(accept.ManifestHash) ||
            string.IsNullOrWhiteSpace(accept.AuthorizedBy) || accept.ReviewedAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Explicit reviewer, approval, manifest and UTC review are required."));
        if (command is RevokeAccountQualificationCommand revoke && (string.IsNullOrWhiteSpace(revoke.Reason) ||
            string.IsNullOrWhiteSpace(revoke.AuthorizedBy) || revoke.RevokedAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Revocation reason, reviewer and UTC time are required."));
        if (command is SetManualTradingHoldCommand hold && (string.IsNullOrWhiteSpace(hold.Reason) ||
            hold.EffectiveAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Hold reason and UTC time are required."));
        if (command is ReleaseManualTradingHoldCommand release && (string.IsNullOrWhiteSpace(release.Reason) ||
            release.EffectiveAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Release reason and UTC time are required."));
        if (command is RequestBrokerAccountResynchronizationCommand resync && (resync.RequestedAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("UTC resynchronization time is required."));

        if (command is RecordBrokerAccountSnapshotCommand accountSnapshot)
        {
            errors.ValidateAccountSnapshot(accountSnapshot.Snapshot);
            if (!Enum.IsDefined(accountSnapshot.Environment)) errors.Add(new("BrokerAccount.Environment is invalid."));
        }

        if (command.Subject.EntityId != command.EntityId.Format()) errors.Add(new("Subject.EntityId must match EntityId."));
        return errors;
    }
}
