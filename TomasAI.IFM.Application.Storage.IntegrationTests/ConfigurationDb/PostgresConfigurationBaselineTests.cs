using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Rebuild;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using static TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb.StrategyCatalogContractTests;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresConfigurationBaselineTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Persisted_configuration_baseline_rebuilds_every_row_with_catalog_guards_and_original_sequence()
    {
        var sourceSettings = new TomasAI.IFM.Shared.Storage.DbConnectionSettings();
        sourceSettings.Add(ConfigurationDbContext.ConfigurationDbConnection,fixture.Connection,"System.Data.Postgres");
        sourceSettings.Add(MarketDataDbContext.MarketDataDbConnection,fixture.Connection,"System.Data.Postgres");
        await new ConfigurationSchemaDb(sourceSettings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new MarketDataSchemaDb(sourceSettings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var factory=new Mock<IDbContextFactory>();
        var context=new ConfigurationDbContext(sourceSettings,factory.Object,NullLogger<DbProvider>.Instance);
        factory.SetupGet(x=>x.ConfigurationDb).Returns(context);
        var published=Definition(StrategyCatalogKind.Family);
        var retired=Definition(StrategyCatalogKind.Family);
        var now=new DateTime(DateTime.UtcNow.Ticks/10*10,DateTimeKind.Utc);
        foreach(var d in new[]{published,retired})
        {
            var digest=await context.InsertStrategyCatalogDraftAsync(d,0,"baseline-test");
            await context.PublishStrategyCatalogAsync(d.Key,digest,now,"baseline-test");
        }
        var retiredRow=await context.GetStrategyCatalogAsync(retired.Key);
        await context.RetireStrategyCatalogAsync(retired.Key,retiredRow!.ContentHash,now.AddMinutes(1),"baseline-test");
        await fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>{await tx.ExecuteAsync("SELECT setval('reference_configuration.lookup_definition_id_seq',900000,true);",[],token);return true;});
        var baseline=new ConfigurationProjectionBaseline(fixture.Transactions);
        await Assert.ThrowsAsync<ArgumentException>(()=>baseline.CaptureAsync(false));
        var captured=await baseline.CaptureAsync(true);
        var source=await baseline.ReadLatestAsync();
        source.Should().NotBeNull(); source!.Tables.Should().BeEquivalentTo(captured.Tables);
        source.LookupSequenceLastValue.Should().Be(900000); source.LookupSequenceIsCalled.Should().BeTrue();
        var name="ifm_config_verify_"+Guid.NewGuid().ToString("N");
        var targetConnection=new NpgsqlConnectionStringBuilder(fixture.Connection){Database=name}.ConnectionString;
        await using var admin=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection);
        await admin.OpenAsync();
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name+";",admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,targetConnection,"System.Data.Postgres")
                .Add(ConfigurationDbContext.ConfigurationDbConnection,targetConnection,"System.Data.Postgres")
                .Add(MarketDataDbContext.MarketDataDbConnection,targetConnection,"System.Data.Postgres");
            await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
            await ConfigurationSchemaDb.WithoutLookupDefaults(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
            await new MarketDataSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
            var transactions=new PostgresEventTransaction(settings);
            var target=new ConfigurationProjectionBaseline(transactions);
            await Assert.ThrowsAsync<System.IO.InvalidDataException>(()=>target.MaterializeAsync(source with {Tables=source.Tables.Select((x,i)=>i==0?x with {RowsJson="[]"}:x).ToArray()}));
            await target.MaterializeAsync(source);
            await target.MaterializeAsync(source);
            foreach(var image in source.Tables)
            {
                var table=image.Name.Contains('.',StringComparison.Ordinal)?image.Name:"reference_configuration."+image.Name;
                var rows=await transactions.ExecuteOnceAsync((tx,token)=>tx.ScalarAsync("SELECT COALESCE(jsonb_agg(j ORDER BY j::text),'[]'::jsonb)::text FROM (SELECT to_jsonb(t) j FROM "+table+" t) q;",[],token));
                rows.Should().Be(image.RowsJson,image.Name);
            }
            var sequence=await transactions.ExecuteOnceAsync((tx,token)=>tx.ScalarAsync("SELECT nextval('reference_configuration.lookup_definition_id_seq');",[],token));
            sequence.Should().Be(900001L);
            await Assert.ThrowsAsync<PostgresException>(()=>transactions.ExecuteOnceAsync(async(tx,token)=>{await tx.ExecuteAsync("DELETE FROM reference_configuration.strategy_catalog_version WHERE id=$1;",[published.Key.Id],token);return true;}));
        }
        finally
        {
            await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE);",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
