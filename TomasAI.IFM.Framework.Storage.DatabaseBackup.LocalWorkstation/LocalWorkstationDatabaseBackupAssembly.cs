using System.Reflection;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation;

/// <summary>
/// Provides access to the LocalWorkstation database-backup adapter assembly.
/// </summary>
public static class LocalWorkstationDatabaseBackupAssembly
{
    /// <summary>
    /// Gets the LocalWorkstation database-backup adapter assembly.
    /// </summary>
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
