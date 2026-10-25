using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
/// <summary>Creates the PostgreSQL ReferenceDb schema, tables and indexes.</summary>
/// <param name = "connectionSettings">The named PostgreSQL connection settings.</param>
/// <param name = "logger">The schema-provider logger.</param>
public sealed class ReferenceSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger) : SchemaDbContext<ReferenceSchemaDb>(connectionSettings[ReferenceDbContext.ReferenceDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects = [new("reference", ReferenceSchemaSql.Create, ReferenceSchemaSql.Drop)];
    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
