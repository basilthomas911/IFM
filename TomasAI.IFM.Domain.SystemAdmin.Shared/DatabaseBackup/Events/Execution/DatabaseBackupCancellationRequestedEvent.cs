using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
[MessagePackObject] public sealed record DatabaseBackupCancellationRequestedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "BackupCancellationRequested"; }
