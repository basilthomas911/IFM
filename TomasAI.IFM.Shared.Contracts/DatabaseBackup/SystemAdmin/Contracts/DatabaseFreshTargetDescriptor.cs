using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseFreshTargetDescriptor(
    [property: Key(0)] string Profile,
    [property: Key(1)] string LogicalTarget,
    [property: Key(2)] DateTimeOffset? RecoveryTargetUtc = null,
    [property: Key(3)] DatabaseArtifactReplicaId? PreferredReplicaId = null);
