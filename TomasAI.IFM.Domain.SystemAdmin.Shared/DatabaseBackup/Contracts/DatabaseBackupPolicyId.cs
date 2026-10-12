using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public readonly record struct DatabaseBackupPolicyId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseBackupPolicyId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}
