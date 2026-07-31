using System.Reflection;

namespace TomasAI.IFM.Application.Storage.Backup;

/// <summary>
/// Provides access to the Storage Backup assembly.
/// </summary>
public static class StorageBackupAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
