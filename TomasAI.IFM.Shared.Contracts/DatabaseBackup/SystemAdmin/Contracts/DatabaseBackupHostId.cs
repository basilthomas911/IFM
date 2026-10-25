using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public readonly record struct DatabaseBackupHostId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseBackupHostId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}
