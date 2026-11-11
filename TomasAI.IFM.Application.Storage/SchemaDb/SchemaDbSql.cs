namespace TomasAI.IFM.Application.Storage.SchemaDb;

/// <summary>Contains read-only SQL used by the shared schema executor.</summary>
internal static class SchemaDbSql
{
    /// <summary>The identifier comes exclusively from the checked-in, regex-qualified ManagedTables metadata.</summary>
    internal static string ReadOneRow(string managedTable) => $"SELECT * FROM {managedTable} LIMIT 1;";
}
