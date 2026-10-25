using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseArtifactReplicaDescriptor
{
    [Key(0)] public DatabaseArtifactId ArtifactId { get; init; }
    [Key(1)] public DatabaseArtifactReplicaId ReplicaId { get; init; }
    [Key(2)] public DatabaseEngine Engine { get; init; }
    [Key(3)] public DatabaseArtifactReplicaState State { get; init; }
    [Key(4)] public string SafeDestinationReference { get; init; } = string.Empty;
    [Key(5)] public long? Bytes { get; init; }
}
