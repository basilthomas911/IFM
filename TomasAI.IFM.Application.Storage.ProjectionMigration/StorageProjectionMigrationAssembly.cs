using System.Reflection;

namespace TomasAI.IFM.Application.Storage.ProjectionMigration;

/// <summary>
/// Provides access to the storage projection-migration assembly.
/// </summary>
public static class StorageProjectionMigrationAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
