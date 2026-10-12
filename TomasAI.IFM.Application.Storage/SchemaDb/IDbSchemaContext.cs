namespace TomasAI.IFM.Application.Storage.SchemaDb;

/// <summary>
/// Manages database objects for one configured storage context.
/// </summary>
public interface IDbSchemaContext
{
    IReadOnlyList<string> ManagedObjects { get; }
    /// <summary>Gets the actual table names from resolved checked-in schema definitions, including generated catalog tables.</summary>
    IReadOnlyList<string> ManagedTables { get; }
    Task<int> ValidateTablesAsync(CancellationToken cancellationToken = default);
    Task CreateAllAsync();
    Task DropAllAsync();
}
