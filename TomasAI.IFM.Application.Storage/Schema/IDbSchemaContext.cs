namespace TomasAI.IFM.Application.Storage.Schema;

/// <summary>
/// Manages database objects for one configured storage context.
/// </summary>
public interface IDbSchemaContext
{
    IReadOnlyList<string> ManagedObjects { get; }
    Task CreateAllAsync();
    Task DropAllAsync();
}
