using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;

[MessagePackObject] public sealed record DatabaseBackupRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupRequested"; }
[MessagePackObject] public sealed record DatabaseBackupAuthorizedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupAuthorized"; }
[MessagePackObject] public sealed record DatabaseBackupExecutionRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseRestoreRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreRequested"; }
[MessagePackObject] public sealed record DatabaseRestoreAuthorizedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreAuthorized"; }
[MessagePackObject] public sealed record DatabaseRestoreExecutionRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseRestoreDrillRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreDrillRequested"; }
[MessagePackObject] public sealed record DatabaseRestoreDrillAuthorizedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreDrillAuthorized"; }
[MessagePackObject] public sealed record DatabaseRestoreDrillExecutionRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreDrillExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseRetentionRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RetentionRequested"; }
[MessagePackObject] public sealed record DatabaseRetentionAuthorizedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RetentionAuthorized"; }
[MessagePackObject] public sealed record DatabaseRetentionExecutionRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RetentionExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseCutoverRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "CutoverRequested"; }
[MessagePackObject] public sealed record DatabaseCutoverAuthorizedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "CutoverAuthorized"; }
[MessagePackObject] public sealed record DatabaseCutoverExecutionRequestedDomainEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "CutoverExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseOperationAdmissionRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "AdmissionRecorded"; }
[MessagePackObject] public sealed record DatabaseOperationStartedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "OperationStarted"; }
[MessagePackObject] public sealed record DatabaseOperationProgressRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "ProgressRecorded"; }
[MessagePackObject] public sealed record DatabaseBackupBoundaryRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BoundaryRecorded"; }
[MessagePackObject] public sealed record DatabaseArtifactReplicaRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "ArtifactReplicaRecorded"; }
[MessagePackObject] public sealed record DatabaseOperationVerificationRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "VerificationRecorded"; }
[MessagePackObject] public sealed record DatabaseRestoreValidationRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreValidationRecorded"; }
[MessagePackObject] public sealed record DatabaseRestoreReadyForCutoverRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "ReadyForCutoverRecorded"; }
[MessagePackObject] public sealed record DatabaseOperationErrorRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "ErrorRecorded"; }
[MessagePackObject] public sealed record DatabaseRecoveryStatisticsRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "StatisticsRecorded"; }
[MessagePackObject] public sealed record DatabaseOperationCompletedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "OperationCompleted"; }
[MessagePackObject] public sealed record DatabaseOperationFailedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "OperationFailed"; }
[MessagePackObject] public sealed record DatabaseOperationCancelledEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "OperationCancelled"; }
[MessagePackObject] public sealed record DatabaseBackupSetCheckpointRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupSetCheckpointRecorded"; }
[MessagePackObject] public sealed record DatabaseBackupSetCompletedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupSetCompleted"; }
[MessagePackObject] public sealed record DatabaseBackupPolicyRevisedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "PolicyRevised"; }
[MessagePackObject] public sealed record DatabaseBackupPolicyEnforcedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "PolicyEnforced"; }
[MessagePackObject] public sealed record DatabaseBackupLegalHoldPlacedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "LegalHoldPlaced"; }
[MessagePackObject] public sealed record DatabaseBackupLegalHoldReleasedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "LegalHoldReleased"; }
[MessagePackObject] public sealed record DatabaseBackupServiceCapabilityRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "ServiceCapabilityRecorded"; }
[MessagePackObject] public sealed record DatabaseBackupServiceReconciledEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "ServiceReconciled"; }
