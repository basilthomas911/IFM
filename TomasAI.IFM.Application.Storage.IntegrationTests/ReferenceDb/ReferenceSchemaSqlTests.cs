using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

public sealed class ReferenceSchemaSqlTests
{
    [Fact]
    public void Reference_schema_catalog_uses_PostgreSql_tables_and_indexes()
    {
        Assert.Contains("CREATE SCHEMA IF NOT EXISTS reference;", ReferenceSchemaSql.Create);
        Assert.Contains("CREATE TABLE IF NOT EXISTS reference.lookup_type", ReferenceSchemaSql.Create);
        Assert.Contains("CREATE TABLE IF NOT EXISTS reference.reference_projection_state", ReferenceSchemaSql.Create);
        Assert.Contains("CREATE INDEX", ReferenceSchemaSql.Create);
        Assert.DoesNotContain("CLUSTERING ORDER", ReferenceSchemaSql.Create);
        Assert.Null(typeof(ReferenceSchemaSql).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.ReferenceDb.Schema.ReferenceSchemaCql"));
    }
}
