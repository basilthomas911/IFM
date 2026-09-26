using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
[MessagePackObject] public sealed record DatabaseOperationProgressRecordedEvent : DatabaseBackupEventContract { [IgnoreMember] public override string Verb => "ProgressRecorded"; }
