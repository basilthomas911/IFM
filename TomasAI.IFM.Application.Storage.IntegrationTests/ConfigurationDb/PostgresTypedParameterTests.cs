using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MessagePack;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresTypedParameterTests(PostgresTradePlanFixture fixture)
{
    async Task<ConfigurationDbContext> ContextAsync()
    {
        var settings = new TomasAI.IFM.Shared.Storage.DbConnectionSettings();
        settings.Add(ConfigurationDbContext.ConfigurationDbConnection,fixture.Connection,"System.Data.Postgres");
        await new ConfigurationSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        return new(settings,new Mock<IDbContextFactory>().Object,NullLogger<DbProvider>.Instance);
    }

    [Fact]
    public async Task Historical_source_events_migrate_all_eight_families_without_rewriting_references_and_resume_idempotently()
    {
        var context=await ContextAsync();var factory=new Mock<IDbContextFactory>();factory.SetupGet(x=>x.ConfigurationDb).Returns(context);
        var originals=new List<ParameterSetVersion>();
        foreach(var union in typeof(ParameterSetValue).GetCustomAttributes(typeof(UnionAttribute),false).Cast<UnionAttribute>())
        {
            var concrete=Activator.CreateInstance(union.SubType.GetProperty("Value")!.PropertyType)!;
            var component=((ParameterSetValue)Activator.CreateInstance(union.SubType,concrete)!).ComponentCode;
            var id=Guid.NewGuid();var values=ParameterComponentModelRegistry.Get(component).CreateTypedDraft(id).WithIdentity(id,1);
            var payload=System.Text.Json.JsonSerializer.Serialize(values.GetType().GetProperty("Value")!.GetValue(values));
            var version=new ParameterSetVersion(new(id,1,component,ParameterCanonicalPayloadModel.Hash(payload)),"History "+id,"",values.SchemaVersion,
                ParameterVersionStatus.Draft,payload,new DateTime(2026,10,1,12,34,56,DateTimeKind.Utc).AddTicks(1234),"migration-test",CatalogRevision:1);
            originals.Add(version);
            var fact=new ParameterSetCreatedEvent {CommandId=Guid.NewGuid(),EntityId=new(id),Revision=1,RequestHash=new string('f',64),
                VersionJson=System.Text.Json.JsonSerializer.Serialize(version)};
            await fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>{await tx.AppendAsync("migration-"+id,fact.CommandId,fact,0,token);return true;});
        }
        var migration=new TomasAI.IFM.Domain.Reference.ParameterSets.Migration.ParameterCatalogMigrationService(fixture.Transactions,factory.Object);
        Assert.True(await migration.ExecuteAsync()>=8);
        Assert.Equal(0,await migration.ExecuteAsync());
        foreach(var original in originals)
        {
            var restored=Assert.Single(await context.GetParameterSetsAsync(original.Reference.ComponentCode,original.Reference.SetId));
            Assert.Equal(original.Reference,restored.Reference);Assert.Equal(original.CreatedAtUtc,restored.CreatedAtUtc);
            Assert.Equal("sorted-json-v1",restored.ReferenceCodec);Assert.Empty(restored.PayloadJson);Assert.NotNull(restored.Parameters);
            var verified=TomasAI.IFM.Domain.Reference.ParameterSets.Migration.ParameterLegacyUpcaster.Verify(original);
            Assert.Equal(ParameterValueCodec.Encode(verified.Parameters),ParameterValueCodec.Encode(restored.Parameters));
        }
    }

    [Fact]
    public async Task Every_family_persists_as_typed_columns_with_exact_binary_round_trip()
    {
        var context=await ContextAsync();
        foreach(var union in typeof(ParameterSetValue).GetCustomAttributes(typeof(UnionAttribute),false).Cast<UnionAttribute>())
        {
            var concrete=Activator.CreateInstance(union.SubType.GetProperty("Value")!.PropertyType)!;
            var component=((ParameterSetValue)Activator.CreateInstance(union.SubType,concrete)!).ComponentCode;
            var value=ParameterComponentModelRegistry.Get(component).CreateTypedDraft(Guid.NewGuid()).WithIdentity(Guid.NewGuid(),1);
            await fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>
            {
                await context.InsertTypedParameterValueAsync(tx,value,token);
                await context.InsertTypedParameterValueAsync(tx,value,token);
                return true;
            });
            var restored=await fixture.Transactions.ExecuteOnceAsync((tx,token)=>context.GetTypedParameterValueAsync(tx,value.SetId,1,token));
            Assert.NotNull(restored);
            Assert.Equal(ParameterValueCodec.Encode(value),ParameterValueCodec.Encode(restored));
        }
        var documentColumns=await fixture.Transactions.ExecuteOnceAsync((tx,token)=>tx.QueryAsync(
            "SELECT table_name,column_name FROM information_schema.columns WHERE table_schema='reference_configuration' AND table_name=ANY($1) AND data_type IN ('json','jsonb','bytea');",
            [ParameterValueMapping.Tables.Select(x=>x.Name).Append("parameter_value_version").ToArray()],reader=>reader.GetString(0)+"."+reader.GetString(1),token));
        Assert.Empty(documentColumns);
    }

    [Fact]
    public async Task Rollback_conflict_detection_and_corruption_detection_preserve_immutable_values()
    {
        var context=await ContextAsync();
        var value=ParameterSetValue.From(new IronCondorMarketSelectionParameterSet
        {
            ParameterSetId=Guid.NewGuid(),Version=1,
            Symbols=[new(){Symbol="ES",CallSpreadWidth=12345678901234567890.123456789m},new(){Symbol="MES",CallSpreadWidth=50m}]
        });
        await Assert.ThrowsAnyAsync<Exception>(()=>fixture.Transactions.ExecuteOnceAsync<bool>(async(tx,token)=>
        {
            await context.InsertTypedParameterValueAsync(tx,value,token);
            await tx.ExecuteAsync("SELECT 1/0;",[],token);
            return true;
        }));
        Assert.Null(await fixture.Transactions.ExecuteOnceAsync((tx,token)=>context.GetTypedParameterValueAsync(tx,value.SetId,1,token)));
        await fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>{await context.InsertTypedParameterValueAsync(tx,value,token);return true;});
        var changed=ParameterSetValue.From(value.Get<IronCondorMarketSelectionParameterSet>() with{DefaultSymbol="MES"});
        var conflict=await Assert.ThrowsAsync<InvalidOperationException>(()=>fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>{await context.InsertTypedParameterValueAsync(tx,changed,token);return true;}));
        Assert.Equal("PARAM.IMMUTABLE_CONTENT_CONFLICT",conflict.Message);
        var restored=await fixture.Transactions.ExecuteOnceAsync((tx,token)=>context.GetTypedParameterValueAsync(tx,value.SetId,1,token));
        Assert.Equal(ParameterValueCodec.Encode(value),ParameterValueCodec.Encode(restored!));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>fixture.Transactions.ExecuteOnceAsync(async(tx,token)=>
        {
            var table=ParameterValueMapping.TablesFor(value.ComponentCode).Single(x=>x.Name.EndsWith("_symbols",StringComparison.Ordinal));
            await tx.ExecuteAsync($"DELETE FROM reference_configuration.{table.Name} WHERE owner_set_id=$1 AND ordinal=0;",[value.SetId],token);
            return await context.GetTypedParameterValueAsync(tx,value.SetId,1,token);
        }));
        restored=await fixture.Transactions.ExecuteOnceAsync((tx,token)=>context.GetTypedParameterValueAsync(tx,value.SetId,1,token));
        Assert.Equal(ParameterValueCodec.Encode(value),ParameterValueCodec.Encode(restored!));
    }
}
