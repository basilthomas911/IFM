using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
[MessagePackObject] public sealed record DatabaseRetentionExecutionRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "RetentionExecutionRequested"; }
