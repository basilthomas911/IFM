using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
[MessagePackObject] public sealed record DatabaseRestoreServiceFailedEvent : DatabaseBackupServiceEventContract { [IgnoreMember] public override string Verb => "RestoreFailed"; }
