using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public readonly record struct DatabaseArtifactReplicaId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseArtifactReplicaId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}
