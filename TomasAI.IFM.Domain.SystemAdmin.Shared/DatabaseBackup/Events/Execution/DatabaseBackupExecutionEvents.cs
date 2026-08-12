using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

[MessagePackObject] public sealed record DatabaseBackupExecutionRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseBackupCancellationRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupCancellationRequested"; }
[MessagePackObject] public sealed record DatabaseBackupVerificationRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupVerificationRequested"; }
[MessagePackObject] public sealed record DatabaseRestoreExecutionRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseRestoreCancellationRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreCancellationRequested"; }
[MessagePackObject] public sealed record DatabaseRestoreDrillRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RestoreDrillRequested"; }
[MessagePackObject] public sealed record DatabaseCutoverExecutionRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "CutoverExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseRetentionEvaluationRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RetentionEvaluationRequested"; }
[MessagePackObject] public sealed record DatabaseRetentionExecutionRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RetentionExecutionRequested"; }
[MessagePackObject] public sealed record DatabaseBackupPolicyActivatedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupPolicyActivated"; }
[MessagePackObject] public sealed record DatabaseBackupReconciliationRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupReconciliationRequested"; }
