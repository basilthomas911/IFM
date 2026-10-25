namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseNativeBackupKind
{
    None = 0,
    PostgreSqlBase = 1,
    PostgreSqlIncremental = 2,
    ScyllaManagerSnapshot = 3,
    ScyllaManagerDeduplicatedSnapshot = 4
}
