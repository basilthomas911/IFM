using TomasAI.IFM.Domain.SystemAdmin.Shared;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public static class SampleData
{
    public const string DatabaseName = DatabaseBackupNames.EventDb;
    public const DatabaseBackupType BackupType = DatabaseBackupType.Full;
    public const int CommandTimeout = 300;

    public const string AlternateDatabaseName = DatabaseBackupNames.TradeDb;
    public const DatabaseBackupType AlternateBackupType = DatabaseBackupType.Diff;
    public const int AlternateCommandTimeout = 600;
}
