using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;

/// <summary>Shared non-serialized fields of the direct DatabaseBackup commands.</summary>
public interface IDatabaseBackupCommand : ICommand<DatabaseRecoveryOperationId>
{
    string Verb { get; }
    DatabaseRequestEnvelope Request { get; init; }
    BackupSource Source { get; init; }
    DatabaseProtectionSetId? ProtectionSetId { get; init; }
    DatabaseConsistencyMode ConsistencyMode { get; init; }
    DatabaseLogicalDestination[] RequiredDestinations { get; init; }
    long ExpectedPolicyRevision { get; init; }
    long ExpectedStateRevision { get; init; }
    string SafeReason { get; init; }
    DatabaseRestorePointId? RestorePointId { get; init; }
    DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    DatabaseRestoreClass RestoreClass { get; init; }
    long ExpectedManifestRevision { get; init; }
    string ApprovalIdentity { get; init; }
    string ApprovalReference { get; init; }
    long ValidationRevision { get; init; }
    string DisposableTargetProfile { get; init; }
    string ValidationProfile { get; init; }
    DatabaseBackupPolicyId? PolicyId { get; init; }
    DatabaseBackupPolicyDefinition? Policy { get; init; }
    DatabaseBackupSetId? BackupSetId { get; init; }
    string LegalHoldReference { get; init; }
    long ExpectedLegalHoldRevision { get; init; }
    DateTimeOffset EvaluationBoundaryUtc { get; init; }
    DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    long RetentionPlanRevision { get; init; }
    DatabaseBackupMode RequestedBackupMode { get; init; }
    void Validate();
}

/// <summary>Validates the direct command contracts without a legacy message adapter.</summary>
public static class DatabaseBackupCommandRules
{
    /// <summary>Validates common fields and the command-specific business requirements.</summary>
    public static void Validate(IDatabaseBackupCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Request.Validate();
        if (command.CommandId == Guid.Empty || command.EntityId.Value == Guid.Empty)
            throw new ArgumentException("Command and entity IDs are required.");
        if (command.RequiredDestinations.Length > DatabaseBackupContractLimits.MaximumCollectionCount)
            throw new ArgumentOutOfRangeException(nameof(command.RequiredDestinations));
        if (command.ExpectedPolicyRevision < 0 || command.ExpectedStateRevision < 0 || command.ExpectedManifestRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(command.ExpectedStateRevision));
        if (command.Source != BackupSource.None) DatabaseBackupEnumValidation.RequireConcrete(command.Source);
        DatabaseBackupEnumValidation.RequireOptionalDefined(command.ConsistencyMode, nameof(command.ConsistencyMode));
        DatabaseBackupEnumValidation.RequireOptionalDefined(command.RestoreClass, nameof(command.RestoreClass));
        DatabaseBackupEnumValidation.RequireOptionalDefined(command.RequestedBackupMode, nameof(command.RequestedBackupMode));

        switch (command)
        {
            case RequestDatabaseBackupCommand:
                RequireSourceAndProtectionSet(command);
                DatabaseBackupEnumValidation.RequireDefined(command.ConsistencyMode, nameof(command.ConsistencyMode));
                if (command.RequiredDestinations.Length == 0) throw new ArgumentException("At least one logical destination is required.");
                foreach (var destination in command.RequiredDestinations) _ = new DatabaseArtifactReplicaId(destination.Name);
                break;
            case CancelDatabaseBackupCommand or CancelDatabaseRestoreCommand:
                SafeText(command.SafeReason, nameof(command.SafeReason));
                break;
            case RequestDatabaseRestoreCommand:
                RequireSourceAndProtectionSet(command);
                if (command.RestorePointId is null || command.FreshTarget is null) throw new ArgumentException("Restore point and fresh target are required.");
                _ = new DatabaseProtectionSetId(command.FreshTarget.Profile);
                _ = new DatabaseProtectionSetId(command.FreshTarget.LogicalTarget);
                DatabaseBackupEnumValidation.RequireDefined(command.RestoreClass, nameof(command.RestoreClass));
                break;
            case ApproveDatabaseRestoreCommand:
                SafeText(command.ApprovalIdentity, nameof(command.ApprovalIdentity));
                SafeText(command.ApprovalReference, nameof(command.ApprovalReference));
                break;
            case ApproveDatabaseCutoverCommand:
                SafeText(command.ApprovalIdentity, nameof(command.ApprovalIdentity));
                SafeText(command.ApprovalReference, nameof(command.ApprovalReference));
                if (command.ValidationRevision <= 0) throw new ArgumentOutOfRangeException(nameof(command.ValidationRevision));
                break;
            case RequestDatabaseRestoreDrillCommand:
                RequireSourceAndProtectionSet(command);
                if (command.RestorePointId is null) throw new ArgumentException("Restore point is required.");
                _ = new DatabaseProtectionSetId(command.DisposableTargetProfile);
                _ = new DatabaseProtectionSetId(command.ValidationProfile);
                break;
            case UpdateDatabaseBackupPolicyCommand:
                if (command.PolicyId is null || command.Policy is null) throw new ArgumentException("Policy identity and definition are required.");
                var policy = command.Policy;
                if (policy.EnabledSources.Length == 0 || policy.EnabledSources.Length > DatabaseBackupContractLimits.MaximumCollectionCount ||
                    policy.ProtectedSets.Length == 0 || policy.ProtectedSets.Length > DatabaseBackupContractLimits.MaximumCollectionCount ||
                    policy.Verification.Levels.Length > DatabaseBackupContractLimits.MaximumCollectionCount)
                    throw new ArgumentOutOfRangeException(nameof(command.Policy));
                foreach (var source in policy.EnabledSources) DatabaseBackupEnumValidation.RequireConcrete(source);
                foreach (var level in policy.Verification.Levels) DatabaseBackupEnumValidation.RequireDefined(level, nameof(policy.Verification.Levels));
                break;
            case PlaceBackupLegalHoldCommand:
                RequireHoldScope(command);
                SafeText(command.SafeReason, nameof(command.SafeReason));
                SafeText(command.LegalHoldReference, nameof(command.LegalHoldReference));
                break;
            case ReleaseBackupLegalHoldCommand:
                RequireHoldScope(command);
                SafeText(command.LegalHoldReference, nameof(command.LegalHoldReference));
                break;
            case RequestBackupRetentionEvaluationCommand:
                DatabaseBackupEnumValidation.RequireConcrete(command.Source);
                DatabaseRequestEnvelope.RequireUtc(command.EvaluationBoundaryUtc, nameof(command.EvaluationBoundaryUtc));
                break;
            case ExecuteBackupRetentionPlanCommand:
                DatabaseBackupEnumValidation.RequireConcrete(command.Source);
                if (command.RetentionPlanId is null || command.RetentionPlanRevision <= 0)
                    throw new ArgumentException("A revision-bound retention plan is required.");
                SafeText(command.ApprovalReference, nameof(command.ApprovalReference));
                break;
            default: throw new ArgumentException("Unsupported DatabaseBackup command.", nameof(command));
        }
    }

    static void RequireSourceAndProtectionSet(IDatabaseBackupCommand command)
    {
        DatabaseBackupEnumValidation.RequireConcrete(command.Source);
        if (command.ProtectionSetId is null) throw new ArgumentException("Protection set is required.", nameof(command.ProtectionSetId));
    }
    static void RequireHoldScope(IDatabaseBackupCommand command)
    {
        if (command.RestorePointId is null && command.BackupSetId is null)
            throw new ArgumentException("A restore point or backup set scope is required.");
    }
    static void SafeText(string value, string parameterName) => DatabaseRequestEnvelope.ValidateSafeText(value, parameterName);
}
