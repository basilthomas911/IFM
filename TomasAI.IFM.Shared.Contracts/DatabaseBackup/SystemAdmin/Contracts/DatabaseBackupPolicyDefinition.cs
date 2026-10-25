using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
[MessagePackObject]
public sealed record DatabaseBackupPolicyDefinition(
    [property: Key(0)] BackupSource[] EnabledSources,
    [property: Key(1)] DatabaseProtectionSetId[] ProtectedSets,
    [property: Key(2)] DatabaseRecoveryObjectives RecoveryObjectives,
    [property: Key(3)] DatabaseRetentionPolicy Retention,
    [property: Key(4)] DatabaseVerificationPolicy Verification);
