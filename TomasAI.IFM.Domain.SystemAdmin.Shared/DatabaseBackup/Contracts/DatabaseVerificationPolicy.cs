using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
[MessagePackObject]
public sealed record DatabaseVerificationPolicy([property: Key(0)] DatabaseVerificationLevel[] Levels, [property: Key(1)] TimeSpan MaximumVerificationAge);
