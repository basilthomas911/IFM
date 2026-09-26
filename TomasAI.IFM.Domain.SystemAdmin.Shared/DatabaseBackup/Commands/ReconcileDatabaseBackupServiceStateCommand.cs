using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

#pragma warning disable MsgPack005 // Abstract contract base is never serialized directly.

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
[MessagePackObject] public sealed record ReconcileDatabaseBackupServiceStateCommand : DatabaseBackupInternalCommand { [IgnoreMember] public override string Verb => "ReconcileServiceState"; }
