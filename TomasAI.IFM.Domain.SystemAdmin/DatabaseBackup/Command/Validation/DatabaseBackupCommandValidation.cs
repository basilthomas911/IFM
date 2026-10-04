using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseBackupEnvelopeValidation;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Validation;

/// <summary>Accumulates Database Backup command failures without mutating recovery state.</summary>
public static class DatabaseBackupCommandValidation
{
    /// <summary>Checks the common request and every command-specific backup requirement.</summary>
    public static List<ValidationError> ValidateBackupCommand(this List<ValidationError> errors, IDatabaseBackupCommand command)
    {
        errors.ValidateBackupRequest(command.Request);
        Check(command.RequiredDestinations is not null && command.RequiredDestinations.Length <= DatabaseBackupContractLimits.MaximumCollectionCount, "RequiredDestinations exceeds its collection bound or is null.");
        Check(command.ExpectedPolicyRevision >= 0, "ExpectedPolicyRevision cannot be negative.");
        Check(command.ExpectedStateRevision >= 0, "ExpectedStateRevision cannot be negative.");
        Check(command.ExpectedManifestRevision >= 0, "ExpectedManifestRevision cannot be negative.");
        Check(command.Source == BackupSource.None || IsConcrete(command.Source), "Source must be a concrete supported backup source.");
        Check(Enum.IsDefined(command.ConsistencyMode), "ConsistencyMode is invalid.");
        Check(Enum.IsDefined(command.RestoreClass), "RestoreClass is invalid.");
        Check(Enum.IsDefined(command.RequestedBackupMode), "RequestedBackupMode is invalid.");
        switch (command)
        {
            case RequestDatabaseBackupCommand:
                RequireSourceAndProtectionSet();
                Check(command.ConsistencyMode != DatabaseConsistencyMode.None, "ConsistencyMode is required.");
                Check(command.RequiredDestinations is { Length: > 0 }, "At least one logical destination is required.");
                foreach (var destination in command.RequiredDestinations ?? [])
                    Check(destination is not null && IsIdentifier(destination.Name), "Logical destination requires a valid identifier.");
                break;
            case CancelDatabaseBackupCommand or CancelDatabaseRestoreCommand:
                Check(IsSafeText(command.SafeReason), "SafeReason is required and must be bounded safe text.");
                break;
            case RequestDatabaseRestoreCommand:
                RequireSourceAndProtectionSet();
                Check(command.RestorePointId is not null, "RestorePointId is required.");
                Check(command.FreshTarget is not null, "FreshTarget is required.");
                if (command.FreshTarget is { } target)
                {
                    Check(IsIdentifier(target.Profile), "FreshTarget.Profile must be a valid identifier.");
                    Check(IsIdentifier(target.LogicalTarget), "FreshTarget.LogicalTarget must be a valid identifier.");
                }
                Check(command.RestoreClass != DatabaseRestoreClass.None, "RestoreClass is required.");
                break;
            case ApproveDatabaseRestoreCommand or ApproveDatabaseCutoverCommand:
                Check(IsSafeText(command.ApprovalIdentity), "ApprovalIdentity is required and must be bounded safe text.");
                Check(IsSafeText(command.ApprovalReference), "ApprovalReference is required and must be bounded safe text.");
                if (command is ApproveDatabaseCutoverCommand)
                    Check(command.ValidationRevision > 0, "ValidationRevision must be positive.");
                break;
            case RequestDatabaseRestoreDrillCommand:
                RequireSourceAndProtectionSet();
                Check(command.RestorePointId is not null, "RestorePointId is required.");
                Check(IsIdentifier(command.DisposableTargetProfile), "DisposableTargetProfile must be a valid identifier.");
                Check(IsIdentifier(command.ValidationProfile), "ValidationProfile must be a valid identifier.");
                break;
            case UpdateDatabaseBackupPolicyCommand:
                Check(command.PolicyId is not null, "PolicyId is required.");
                ValidatePolicy(errors, command.Policy);
                break;
            case PlaceBackupLegalHoldCommand or ReleaseBackupLegalHoldCommand:
                Check(command.RestorePointId is not null || command.BackupSetId is not null, "A RestorePointId or BackupSetId hold scope is required.");
                Check(IsSafeText(command.LegalHoldReference), "LegalHoldReference is required and must be bounded safe text.");
                if (command is PlaceBackupLegalHoldCommand)
                    Check(IsSafeText(command.SafeReason), "SafeReason is required and must be bounded safe text.");
                break;
            case RequestBackupRetentionEvaluationCommand:
                Check(IsConcrete(command.Source), "A concrete Source is required.");
                Check(command.EvaluationBoundaryUtc != default && command.EvaluationBoundaryUtc.Offset == TimeSpan.Zero, "EvaluationBoundaryUtc must be non-default UTC.");
                break;
            case ExecuteBackupRetentionPlanCommand:
                Check(IsConcrete(command.Source), "A concrete Source is required.");
                Check(command.RetentionPlanId is not null && command.RetentionPlanRevision > 0, "A revision-bound RetentionPlanId is required.");
                Check(IsSafeText(command.ApprovalReference), "ApprovalReference is required and must be bounded safe text.");
                break;
            default:
                Check(false, "Unsupported DatabaseBackup command.");
                break;
        }
        if (command.Subject.EntityId != command.EntityId.Format()) Check(false, "Subject.EntityId must match EntityId.");
        return errors;
        void Check(bool valid, string message) { if (!valid) errors.Add(new($"{command.CommandName}: {message}")); }
        void RequireSourceAndProtectionSet()
        {
            Check(IsConcrete(command.Source), "A concrete Source is required.");
            Check(command.ProtectionSetId is { } id && IsIdentifier(id.Value), "ProtectionSetId is required.");
        }
    }

    /// <summary>Checks sourced internal callbacks and optional lineage without throwing.</summary>
    public static List<ValidationError> ValidateBackupCommand(this List<ValidationError> errors, DatabaseBackupInternalCommand command)
    {
        errors.ValidateBackupSource(command.Source);
        Check(command.Source is not null && command.EntityId == command.Source.OperationId, "EntityId must match Source.OperationId.");
        Check(command.ProgressPercent is >= 0 and <= 100, "ProgressPercent must be between 0 and 100.");
        Check(IsSafeText(command.SafeDiagnosticReference, DatabaseBackupContractLimits.DiagnosticReferenceLength, false), "SafeDiagnosticReference is invalid.");
        Check(Enum.IsDefined(command.VerificationLevel), "VerificationLevel is invalid.");
        Check(Enum.IsDefined(command.Outcome), "Outcome is invalid.");
        Check(Enum.IsDefined(command.ErrorClassification), "ErrorClassification is invalid.");
        Check(Enum.IsDefined(command.CutoverState), "CutoverState is invalid.");
        Check(Enum.IsDefined(command.CapabilityState), "CapabilityState is invalid.");
        Check(Enum.IsDefined(command.RestoreClass), "RestoreClass is invalid.");
        Check(command.EvaluationBoundaryUtc == default || command.EvaluationBoundaryUtc.Offset == TimeSpan.Zero, "EvaluationBoundaryUtc must be UTC.");
        Check(command.ManifestRevision >= 0, "ManifestRevision cannot be negative.");
        if (command.BackupLineage is { } lineage)
        {
            Check(Enum.IsDefined(lineage.RequestedMode), "BackupLineage.RequestedMode is invalid.");
            Check(Enum.IsDefined(lineage.ResolvedMode), "BackupLineage.ResolvedMode is invalid.");
            Check(Enum.IsDefined(lineage.NativeKind), "BackupLineage.NativeKind is invalid.");
            Check(lineage.ChainDepth >= 0, "BackupLineage.ChainDepth cannot be negative.");
            if (lineage.ResolvedMode == DatabaseBackupMode.Full)
            {
                Check(lineage.ParentRestorePointId is null && lineage.ChainDepth == 0, "Full backup cannot have a parent or non-zero ChainDepth.");
                Check(lineage.NativeKind is not (DatabaseNativeBackupKind.PostgreSqlIncremental or DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot), "Full backup cannot use an incremental native kind.");
            }
            if (lineage.ResolvedMode == DatabaseBackupMode.Incremental)
            {
                Check(lineage.BaseRestorePointId is not null && lineage.ParentRestorePointId is not null && lineage.ChainDepth > 0, "Incremental backup requires base, parent and ChainDepth.");
                Check(lineage.NativeKind is DatabaseNativeBackupKind.PostgreSqlIncremental or DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot, "Incremental backup requires an incremental native kind.");
            }
            Check(IsSafeText(lineage.NativeIdentity, required: false), "BackupLineage.NativeIdentity is invalid.");
        }
        if (command.Subject.EntityId != command.EntityId.Format()) Check(false, "Subject.EntityId must match EntityId.");
        return errors;
        void Check(bool valid, string message) { if (!valid) errors.Add(new($"{command.CommandName}: {message}")); }
    }

    /// <summary>Checks every existing policy collection rule, safely handling missing nested payloads.</summary>
    private static void ValidatePolicy(List<ValidationError> errors, DatabaseBackupPolicyDefinition? policy)
    {
        if (policy is null) { errors.Add(new("Policy is required.")); return; }
        if (policy.EnabledSources is not { Length: > 0 and <= DatabaseBackupContractLimits.MaximumCollectionCount }) errors.Add(new("Policy.EnabledSources must contain 1-32 sources."));
        foreach (var source in policy.EnabledSources ?? []) if (!IsConcrete(source)) errors.Add(new("Policy.EnabledSources requires concrete sources."));
        if (policy.ProtectedSets is not { Length: > 0 and <= DatabaseBackupContractLimits.MaximumCollectionCount }) errors.Add(new("Policy.ProtectedSets must contain 1-32 identifiers."));
        foreach (var id in policy.ProtectedSets ?? []) if (!IsIdentifier(id.Value)) errors.Add(new("Policy.ProtectedSets contains an invalid identifier."));
        if (policy.Verification?.Levels is not { Length: <= DatabaseBackupContractLimits.MaximumCollectionCount }) errors.Add(new("Policy.Verification.Levels is required and bounded to 32 levels."));
        foreach (var level in policy.Verification?.Levels ?? []) if (!Enum.IsDefined(level) || level == DatabaseVerificationLevel.None) errors.Add(new("Policy.Verification.Levels requires defined non-zero levels."));
    }

    /// <summary>Tests whether a source is supported for backup and recovery.</summary>
    private static bool IsConcrete(BackupSource source) => source is BackupSource.LocalWorkstation or BackupSource.AwsCloud;
}
