using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public readonly record struct DatabaseBackupSetId
{
    [Key(0)] public Guid Value { get; }
    [SerializationConstructor] public DatabaseBackupSetId(Guid value) => Value = value;
    public override string ToString() => Value.ToString("N");
}
