using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

#pragma warning disable MsgPack005 // Abstract contract bases are never serialized directly.

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

[MessagePackObject]
public abstract record DatabaseBackupServiceEventContract : DatabaseBackupEventContract
{
    [IgnoreMember] public override EventType EventType => EventType.ServiceEvent;
}
