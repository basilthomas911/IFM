using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;

/// <summary>Records a retention result without requesting another deletion execution.</summary>
[MessagePackObject]
public sealed record DatabaseRetentionResultRecordedDomainEvent : DatabaseBackupEventContract
{
    [IgnoreMember] public override string Verb => "RetentionResultRecorded";
}