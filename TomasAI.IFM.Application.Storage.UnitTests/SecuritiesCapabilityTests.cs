using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.UnitTests;

public sealed class SecuritiesCapabilityTests
{
    [Fact]
    public void Rollover_reads_and_mutations_are_on_the_respective_capabilities()
    {
        Assert.True(typeof(IFuturesContractRolloverReadStore).IsAssignableFrom(typeof(ISecuritiesDbReadContext)));
        Assert.True(typeof(IFuturesContractRolloverWriteStore).IsAssignableFrom(typeof(ISecuritiesDbWriteContext)));
        Assert.False(typeof(IFuturesContractRolloverWriteStore).IsAssignableFrom(typeof(ISecuritiesDbReadContext)));
        Assert.True(typeof(IFuturesContractRolloverReadStore).IsAssignableFrom(typeof(IFuturesContractRolloverStore)));
        Assert.True(typeof(IFuturesContractRolloverWriteStore).IsAssignableFrom(typeof(IFuturesContractRolloverStore)));
    }

    [Fact]
    public void Non_PostgreSQL_securities_configuration_is_rejected_before_database_execution()
    {
        var settings = new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection, "Contact Points=127.0.0.1", "ScyllaDb");
        var factory = new DbContextFactory(new DbContextResolver(_ => throw new InvalidOperationException("No connection may be requested.")));
        Assert.Throws<NotSupportedException>(() => new SecuritiesDbContext(settings, factory, NullLogger<DbProvider>.Instance));
        Assert.Throws<NotSupportedException>(() => new SecuritiesSchemaDb(settings, NullLogger<DbProvider>.Instance));
    }

    [Fact]
    public void The_schema_and_assembly_have_no_legacy_securities_CQL_journal()
    {
        Assert.Null(typeof(SecuritiesDbContext).Assembly.GetType("TomasAI.IFM.Application.Storage.SecuritiesDb.SecuritiesDbCql"));
        Assert.Null(typeof(SecuritiesDbContext).Assembly.GetType("TomasAI.IFM.Application.Storage.SecuritiesDb.Schema.SecuritiesSchemaCql"));
        foreach (var table in new[] { "securities_projection_state", "securities_symbol_projection_state", "securities_projection_operation" })
            Assert.DoesNotContain(table, SecuritiesSchemaSql.Create, StringComparison.Ordinal);
        Assert.Contains("securities.futures_contract_by_symbol", SecuritiesSchemaSql.Create, StringComparison.Ordinal);
        Assert.Contains("securities.futures_option_contract_by_symbol", SecuritiesSchemaSql.Create, StringComparison.Ordinal);
    }
}
