using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;

[MessagePackObject] public sealed record DatabaseBackupServiceAcceptedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupAccepted"; }
[MessagePackObject] public sealed record DatabaseBackupServiceRejectedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupRejected"; }
[MessagePackObject] public sealed record DatabaseBackupServiceStartedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupStarted"; }
[MessagePackObject] public sealed record DatabaseBackupServiceProgressEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupProgress"; }
[MessagePackObject] public sealed record DatabaseBackupBoundaryEstablishedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupBoundaryEstablished"; }
[MessagePackObject] public sealed record DatabaseBackupArtifactReplicaUpdatedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupArtifactReplicaUpdated"; }
[MessagePackObject] public sealed record DatabaseBackupVerificationCompletedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupVerificationCompleted"; }
[MessagePackObject] public sealed record DatabaseBackupServiceErrorEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupError"; }
[MessagePackObject] public sealed record DatabaseBackupServiceCompletedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupCompleted"; }
[MessagePackObject] public sealed record DatabaseBackupServiceFailedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupFailed"; }
[MessagePackObject] public sealed record DatabaseBackupServiceCancelledEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "BackupCancelled"; }

[MessagePackObject] public sealed record DatabaseRestoreServiceAcceptedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreAccepted"; }
[MessagePackObject] public sealed record DatabaseRestoreServiceRejectedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreRejected"; }
[MessagePackObject] public sealed record DatabaseRestoreServiceStartedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreStarted"; }
[MessagePackObject] public sealed record DatabaseRestoreServiceProgressEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreProgress"; }
[MessagePackObject] public sealed record DatabaseRestoreValidationCompletedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreValidationCompleted"; }
[MessagePackObject] public sealed record DatabaseRestoreReadyForCutoverEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreReadyForCutover"; }
[MessagePackObject] public sealed record DatabaseRestoreDrillCompletedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreDrillCompleted"; }
[MessagePackObject] public sealed record DatabaseRestoreServiceErrorEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreError"; }
[MessagePackObject] public sealed record DatabaseRestoreServiceCompletedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreCompleted"; }
[MessagePackObject] public sealed record DatabaseRestoreServiceFailedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreFailed"; }
[MessagePackObject] public sealed record DatabaseRestoreServiceCancelledEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreCancelled"; }

[MessagePackObject] public sealed record DatabaseRecoveryRunStatisticsCapturedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RunStatisticsCaptured"; }
[MessagePackObject] public sealed record DatabaseBackupPolicyAppliedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "PolicyApplied"; }
[MessagePackObject] public sealed record DatabaseBackupPolicyRejectedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "PolicyRejected"; }
[MessagePackObject] public sealed record DatabaseRetentionPlanCreatedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RetentionPlanCreated"; }
[MessagePackObject] public sealed record DatabaseRetentionExecutionCompletedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RetentionExecutionCompleted"; }
[MessagePackObject] public sealed record DatabaseRetentionExecutionFailedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RetentionExecutionFailed"; }
[MessagePackObject] public sealed record DatabaseBackupServiceReconciliationEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "ServiceReconciliation"; }
[MessagePackObject] public sealed record DatabaseBackupServiceCapabilityChangedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "ServiceCapabilityChanged"; }
