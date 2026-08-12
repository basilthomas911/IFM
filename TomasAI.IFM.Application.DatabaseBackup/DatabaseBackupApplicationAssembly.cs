using System.Reflection;

namespace TomasAI.IFM.Application.DatabaseBackup;

/// <summary>
/// Provides access to the destination-neutral database-backup application assembly.
/// </summary>
public static class DatabaseBackupApplicationAssembly
{
    /// <summary>
    /// Gets the database-backup application assembly.
    /// </summary>
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
