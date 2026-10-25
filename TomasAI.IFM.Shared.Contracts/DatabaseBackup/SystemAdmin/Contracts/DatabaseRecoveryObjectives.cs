using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseRecoveryObjectives([property: Key(0)] TimeSpan RecoveryPointObjective, [property: Key(1)] TimeSpan RecoveryTimeObjective);
