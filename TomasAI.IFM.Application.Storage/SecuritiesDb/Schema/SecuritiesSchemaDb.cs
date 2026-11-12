using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;

/// <summary>Executes the authoritative PostgreSQL Securities schema.</summary>
/// <param name="connectionSettings">The named PostgreSQL connection.</param>
/// <param name="logger">The schema provider logger.</param>
public sealed class SecuritiesSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<SecuritiesSchemaDb>(connectionSettings[SecuritiesDbContext.SecuritiesDbConnection] is { ProviderName: "System.Data.Postgres" } postgresConnection
        ? postgresConnection : throw new NotSupportedException("Securities schema requires PostgreSQL."), logger)
{
    /// <summary>Gets the securities schema for isolated setup and verification.</summary>
    public static string CreateStatement => SecuritiesSchemaSql.Create;

    static readonly SchemaObjectDefinition[] Objects = [new("securities", SecuritiesSchemaSql.Create, SecuritiesSchemaSql.Drop)];

    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
