using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseRetentionPolicy([property: Key(0)] int DailyCount, [property: Key(1)] int WeeklyCount, [property: Key(2)] int MonthlyCount);
