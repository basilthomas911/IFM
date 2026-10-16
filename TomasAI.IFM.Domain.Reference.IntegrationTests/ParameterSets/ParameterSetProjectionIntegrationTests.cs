using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
namespace TomasAI.IFM.Domain.Reference.IntegrationTests.ParameterSets;

[Collection(ReferenceIntegrationInfrastructureCollection.Name)]
public sealed class ParameterSetProjectionIntegrationTests(ReferenceIntegrationInfrastructureFixture infrastructure)
{
    [Fact, Trait("Category", "Integration")]
    public async Task Projection_is_idempotent_and_rejects_payload_mutation()
    {
        var database="parameter_projection_"+Guid.NewGuid().ToString("N");
        await using(var admin=new TomasAI.IFM.Framework.Storage.Postgres.PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(infrastructure.PostgresConnectionString))
        {
            await admin.OpenAsync();await using var create=new NpgsqlCommand("CREATE DATABASE "+database,admin);await create.ExecuteNonQueryAsync();
        }
        var connection = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString){Database=database}.ConnectionString;
        var settings = new DbConnectionSettings().Add(ConfigurationDbContext.ConfigurationDbConnection, connection, "System.Data.Postgres");
        var factory = Substitute.For<IDbContextFactory>(); var logger = Substitute.For<ILogger<DbProvider>>();
        var store = new ConfigurationDbContext(settings, factory, logger); factory.ConfigurationDb.Returns(store);
        await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
        var components = await store.GetParameterComponentsAsync();
        components.Should().Contain(x => x.ComponentCode == RegimeDiscoveryParameterModel.ComponentCode && x.SchemaVersions.SequenceEqual(new[] { 1, 2, 3, 4, 5 }));
        components.Should().Contain(x =>
         x.AreaCode == "market-data-analytics" &&
         x.AreaName == "Market Data Analytics" &&
         x.ComponentCode == FuturesItiSignalParameterModel.ComponentCode &&
         x.Name == "Future ITI Signal" &&
         x.SchemaVersions.SequenceEqual(new[] { 1 }));
        components.Should().Contain(x =>
            x.AreaCode == "option-spread-strategy" &&
            x.AreaName == "Option Spread Strategy Defaults" &&
            x.ComponentCode == IronCondorMarketSelectionParameterModel.ComponentCode &&
            x.Name == "Iron Condor" &&
            x.SchemaVersions.SequenceEqual(new[] { 1 }));
        components.Should().Contain(x =>
            x.AreaCode == "option-spread-strategy" &&
            x.AreaName == "Option Spread Strategy Defaults" &&
            x.ComponentCode == VerticalSpreadMarketSelectionParameterModel.ComponentCode &&
            x.Name == "Vertical Spreads" &&
            x.SchemaVersions.SequenceEqual(new[] { 1 }));
        var schema = await store.GetParameterSchemaAsync(RegimeDiscoveryParameterModel.ComponentCode, 3);
        schema.Should().NotBeNull(); schema!.SchemaSha256.Should().Be(ParameterSchemaRegistry.Default.Get(RegimeDiscoveryParameterModel.ComponentCode, 3).SchemaSha256);

        var id=Guid.NewGuid();var values=new RegimeDiscoveryParameterModel().CreateTypedDraft(id).WithIdentity(id,1);
        var version=new ParameterSetVersion(new(id,1,values.ComponentCode,ParameterValueCodec.Hash(values)),"Integration","",values.SchemaVersion,
            ParameterVersionStatus.Draft,string.Empty,DateTime.UtcNow,"test",CatalogRevision:1){Parameters=values};
        var fact=new ParameterSetCreatedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=1,RequestHash=new string('a',64),Version=version};
        await store.ProjectParameterSetAsync(fact);await store.ProjectParameterSetAsync(fact);
        var renamed=version with{Name="Renamed",Description="Metadata only",CatalogRevision=2};
        await store.ProjectParameterSetAsync(new ParameterSetRenamedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=2,RequestHash=new string('b',64),Version=renamed});
        var read=Assert.Single(await store.GetParameterSetsAsync(values.ComponentCode,id));
        Assert.Equal("Renamed",read.Name);Assert.Equal(2,read.CatalogRevision);Assert.Empty(read.PayloadJson);
        Assert.Equal(ParameterValueCodec.Encode(values),ParameterValueCodec.Encode(read.Parameters!));
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.ProjectParameterSetAsync(fact with{CommandId=Guid.NewGuid(),Revision=3,Version=version with{Reference=version.Reference with{PayloadSha256=new string('f',64)}}}));
        var published=renamed with{Status=ParameterVersionStatus.Published,PublishedAtUtc=DateTime.UtcNow,CatalogRevision=3};
        await store.ProjectParameterSetAsync(new ParameterVersionPublishedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=3,RequestHash=new string('c',64),Version=published});
        var nextValues=values.WithIdentity(id,2);
        var next=published with{Reference=published.Reference with{Version=2,PayloadSha256=ParameterValueCodec.Hash(nextValues)},Parameters=nextValues,
            Status=ParameterVersionStatus.Draft,PublishedAtUtc=null,CatalogRevision=4};
        await store.ProjectParameterSetAsync(new ParameterDraftSavedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=4,RequestHash=new string('d',64),Version=next});
        var page=Assert.Single(await store.GetParameterSetsAsync(values.ComponentCode,id,limit:1));Assert.Equal(2,page.Reference.Version);
        var second=Assert.Single(await store.GetParameterSetsAsync(values.ComponentCode,id,limit:1,afterName:page.Name,afterSetId:id,afterVersion:2));Assert.Equal(1,second.Reference.Version);
        Assert.Empty(await store.GetParameterSetsAsync(values.ComponentCode,id,limit:1,afterName:second.Name,afterSetId:id,afterVersion:1));
        var retired=published with{Status=ParameterVersionStatus.Retired,RetiredAtUtc=DateTime.UtcNow,CatalogRevision=5};
        await store.ProjectParameterSetAsync(new ParameterVersionRetiredEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=5,RequestHash=new string('e',64),Version=retired});
        Assert.Equal(ParameterVersionStatus.Retired,(await store.GetParameterSetsAsync(values.ComponentCode,id)).Single(x=>x.Reference.Version==1).Status);
        var runId=Guid.NewGuid();var snapshot=new ParameterStartupSnapshotModel(runId,[],new Dictionary<ParameterVersionRef,ParameterSetVersion>());
        var run=new ParameterStartupRun(runId,[],[],SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers()),DateTime.UtcNow,"test");
        var applied=new ParameterStartupChangedEvent{RunId=runId,EntityId=ParameterStartupEntityId.Registry,Revision=1,CommandId=Guid.NewGuid(),Run=run};
        await store.ProjectParameterStartupAsync(applied);await store.ProjectParameterStartupAsync(applied);
        var report=new ParameterSignalStartupReport(runId,run.Plan.Fingerprint,new DateOnly(2026,10,15),"ES",DateTime.UtcNow,[]);
        var recorded=applied with{CommandId=Guid.NewGuid(),Revision=2,Run=null,Report=report};
        await store.ProjectParameterStartupAsync(recorded);await store.ProjectParameterStartupAsync(recorded);
        await Assert.ThrowsAsync<InvalidDataException>(()=>store.ProjectParameterStartupAsync(recorded with{Report=report with{ContractId="CHANGED"}}));
        await store.ProjectParameterStartupAsync(applied with{CommandId=Guid.NewGuid(),Revision=3,Run=null,Released=true});
        await store.ProjectParameterStartupAsync(applied); // Old replay must not reactivate the run.
        await using(var db=store.CreateConnection().As<NpgsqlConnection>(store.ConnectionString))
        {
            await db.OpenAsync();await using var check=db.CreateCommand();
            check.CommandText="SELECT active FROM reference_configuration.parameter_catalog_startup_state WHERE run_id=$1";check.Parameters.AddWithValue(runId);
            Assert.Equal(false,await check.ExecuteScalarAsync());
        }
        var legacyValue=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());
        await store.InsertRegimeDiscoveryDraftAsync(legacyValue,"Explicit historical import","test");
        var legacy=Assert.Single(await store.GetLegacyParameterVersionsAsync(legacyValue.ParameterSetId,legacyValue.Version));
        var imported=ParameterHistoryReader.ReadValues(values.ComponentCode,ParameterLegacyMigrationModel.Expand(legacy));
        var target=ParameterLegacyMigrationModel.TargetId(legacy.Reference);
        var migration=new CreateParameterSetCommand{CommandId=target,EntityId=new(target),Name="Imported",SchemaVersion=imported.SchemaVersion,Parameters=imported,LegacySource=legacy.Reference};
        var migrated=ParameterMutationModel.Decide(migration,0,new Dictionary<int,ParameterSetVersion>(),DateTime.UtcNow);
        await store.ProjectParameterSetAsync(new ParameterSetCreatedEvent{CommandId=target,EntityId=new(target),Revision=1,RequestHash=ParameterMutationModel.RequestHash(migration),Version=migrated});
        Assert.Equal(legacy.Reference,Assert.Single(await store.GetParameterSetsAsync(values.ComponentCode,target)).LegacySource);
        var first=await store.AcquireParameterWriteLeaseAsync();var waiting=store.AcquireParameterWriteLeaseAsync();
        try{await Task.Delay(100);Assert.False(waiting.IsCompleted);}finally{await first.DisposeAsync();}
        await using var acquired=await waiting.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
