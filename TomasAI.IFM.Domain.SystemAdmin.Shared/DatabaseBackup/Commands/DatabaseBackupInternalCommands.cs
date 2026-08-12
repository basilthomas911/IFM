using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

#pragma warning disable MsgPack005 // Abstract contract base is never serialized directly.

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;

[MessagePackObject]
public abstract record DatabaseBackupInternalCommand : ICommand<DatabaseRecoveryOperationId>, IDatabaseBackupValidatable
{
    public const string Actor = "DatabaseBackupCommand";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public DatabaseRecoveryOperationId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = 9190;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.SystemAdminBoundedContext;
    [Key(6)] public DatabaseSourceEnvelope Source { get; init; } = new();
    [Key(7)] public int ProgressPercent { get; init; }
    [Key(8)] public string SafeDiagnosticReference { get; init; } = string.Empty;
    [Key(9)] public DatabaseArtifactReplicaDescriptor? ArtifactReplica { get; init; }
    [Key(10)] public DatabaseVerificationLevel VerificationLevel { get; init; }
    [Key(11)] public DatabaseRecoveryOutcome Outcome { get; init; }
    [Key(12)] public DatabaseErrorClassification ErrorClassification { get; init; }
    [Key(13)] public DatabaseCutoverState CutoverState { get; init; }
    [Key(14)] public DatabaseServiceCapabilityState CapabilityState { get; init; }
    [Key(15)] public DatabaseRecoveryRunStatistics? Statistics { get; init; }
    [Key(16)] public long ExpectedStateRevision { get; init; }
    [Key(17)] public long ValidationRevision { get; init; }
    [Key(18)] public DatabaseRestorePointId? RestorePointId { get; init; }
    [Key(19)] public DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    [Key(20)] public DatabaseRestoreClass RestoreClass { get; init; }
    [Key(21)] public DatabaseBackupPolicyId? PolicyId { get; init; }
    [Key(22)] public DatabaseBackupPolicyDefinition? Policy { get; init; }
    [Key(23)] public DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    [Key(24)] public long RetentionPlanRevision { get; init; }
    [Key(25)] public DateTimeOffset EvaluationBoundaryUtc { get; init; }
    [Key(26)] public long ManifestRevision { get; init; }

    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => "DatabaseBackupEventActor";
    [IgnoreMember] public abstract string Verb { get; }

    public virtual void Validate()
    {
        Source.Validate();
        if (CommandId == Guid.Empty || EntityId != Source.OperationId) throw new ArgumentException("Command identity must match the source operation.");
        if (ProgressPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(ProgressPercent));
        if (SafeDiagnosticReference.Length > DatabaseBackupContractLimits.DiagnosticReferenceLength || SafeDiagnosticReference.Any(char.IsControl)) throw new ArgumentOutOfRangeException(nameof(SafeDiagnosticReference));
        DatabaseBackupEnumValidation.RequireOptionalDefined(VerificationLevel, nameof(VerificationLevel));
        DatabaseBackupEnumValidation.RequireOptionalDefined(Outcome, nameof(Outcome));
        DatabaseBackupEnumValidation.RequireOptionalDefined(ErrorClassification, nameof(ErrorClassification));
        DatabaseBackupEnumValidation.RequireOptionalDefined(CutoverState, nameof(CutoverState));
        DatabaseBackupEnumValidation.RequireOptionalDefined(CapabilityState, nameof(CapabilityState));
        DatabaseBackupEnumValidation.RequireOptionalDefined(RestoreClass, nameof(RestoreClass));
        if (EvaluationBoundaryUtc != default && EvaluationBoundaryUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("EvaluationBoundaryUtc must be UTC.", nameof(EvaluationBoundaryUtc));
        if (ManifestRevision < 0) throw new ArgumentOutOfRangeException(nameof(ManifestRevision));
    }
}

[MessagePackObject] public sealed record RecordDatabaseOperationAdmissionCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordAdmission"; }
[MessagePackObject] public sealed record RecordDatabaseOperationStartedCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordStarted"; }
[MessagePackObject] public sealed record RecordDatabaseOperationProgressCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordProgress"; }
[MessagePackObject] public sealed record RecordDatabaseBackupBoundaryCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordBoundary"; }
[MessagePackObject] public sealed record RecordDatabaseArtifactReplicaCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordArtifactReplica"; }
[MessagePackObject] public sealed record RecordDatabaseOperationVerificationCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordVerification"; }
[MessagePackObject] public sealed record RecordDatabaseOperationErrorCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordError"; }
[MessagePackObject] public sealed record RecordDatabaseRestoreReadyForCutoverCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordReadyForCutover"; }
[MessagePackObject] public sealed record CompleteDatabaseOperationCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "CompleteOperation"; }
[MessagePackObject] public sealed record FailDatabaseOperationCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "FailOperation"; }
[MessagePackObject] public sealed record RecordDatabaseOperationCancelledCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordCancelled"; }
[MessagePackObject] public sealed record RecordDatabaseBackupPolicyStatusCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordPolicyStatus"; }
[MessagePackObject] public sealed record RecordDatabaseRetentionResultCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordRetentionResult"; }
[MessagePackObject] public sealed record ReconcileDatabaseBackupServiceStateCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "ReconcileServiceState"; }
[MessagePackObject] public sealed record RecordDatabaseBackupServiceCapabilityCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordServiceCapability"; }
[MessagePackObject] public sealed record RecordDatabaseRecoveryRunStatisticsCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "RecordRunStatistics"; }
