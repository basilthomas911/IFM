using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public readonly record struct DatabaseRecoveryOperationId : IActorEntityId
{
    [Key(0)] public Guid Value { get; }
    [SerializationConstructor] public DatabaseRecoveryOperationId(Guid value) => Value = value;
    public string Format() => Value.ToString("N");
}
