using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseLogicalDestination([property: Key(0)] string Name, [property: Key(1)] bool Required);
