using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Schema;

/// <summary>
/// Executes checked-in schema definitions against an existing database or keyspace.
/// </summary>
public abstract class SchemaDbContext<TSchemaDb>(
    IDbConnectionSetting connectionSetting,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<TSchemaDb>(connectionSetting, logger), IDbSchemaContext
    where TSchemaDb : IObjectRepository
{
    protected abstract IReadOnlyList<SchemaObjectDefinition> Definitions { get; }

    public override IObjectRepository Database => this;

    public IReadOnlyList<string> ManagedObjects => Definitions.Select(definition => definition.Name).ToArray();

    public async Task CreateAllAsync()
    {
        foreach (var definition in Definitions)
            await Use(definition.CreateStatement).ExecuteCommandAsync().ConfigureAwait(false);
    }

    public async Task DropAllAsync()
    {
        foreach (var definition in Definitions.Reverse())
            await Use(definition.DropStatement).ExecuteCommandAsync().ConfigureAwait(false);
    }
}
