using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Validation;

/// <summary>Structural validation for BrokerAccount commands.</summary>
public static class BrokerAccountCommandValidation
{
    /// <summary>Accumulates deterministic message errors without I/O.</summary>
    public static List<ValidationError> Validate(ICommand command) => new List<ValidationError>()
        .ValidateCommandId(command.CommandId, command.CommandName)
        .CaptureCommandValidation(() =>
        {
            if (command is not BrokerAccountCommand typed || !typed.EntityId.IsValid ||
                command.Subject.EntityId != typed.EntityId.Format())
                throw new ArgumentException("A valid matching BrokerAccount identity is required.");
            switch (typed)
            {
                case RecordBrokerAccountSnapshotCommand snapshot when
                    snapshot.Environment == Application.TradeBroker.Contracts.BrokerEnvironment.Unknown ||
                    snapshot.Snapshot.AccountAlias != typed.EntityId.AccountAlias ||
                    snapshot.Snapshot.Generation <= 0 || snapshot.Snapshot.AsOfUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("A sourced UTC snapshot for the exact account is required.");
                case SubmitAccountQualificationEvidenceCommand evidence when
                    string.IsNullOrWhiteSpace(evidence.ManifestHash) ||
                    string.IsNullOrWhiteSpace(evidence.EvidenceReference) ||
                    evidence.SubmittedAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("Qualification manifest, evidence reference and UTC submission are required.");
                case AcceptAccountQualificationCommand accept when accept.ApprovalId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(accept.ManifestHash) ||
                    string.IsNullOrWhiteSpace(accept.AuthorizedBy) || accept.ReviewedAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("Explicit reviewer, approval, manifest and UTC review are required.");
                case RevokeAccountQualificationCommand revoke when string.IsNullOrWhiteSpace(revoke.Reason) ||
                    string.IsNullOrWhiteSpace(revoke.AuthorizedBy) || revoke.RevokedAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("Revocation reason, reviewer and UTC time are required.");
                case SetManualTradingHoldCommand hold when string.IsNullOrWhiteSpace(hold.Reason) ||
                    hold.EffectiveAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("Hold reason and UTC time are required.");
                case ReleaseManualTradingHoldCommand release when string.IsNullOrWhiteSpace(release.Reason) ||
                    release.EffectiveAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("Release reason and UTC time are required.");
                case RequestBrokerAccountResynchronizationCommand resync when
                    resync.RequestedAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("UTC resynchronization time is required.");
            }
        });
}
