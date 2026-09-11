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
public sealed class ParameterSetProjectionIntegrationTests
{
 [Fact, Trait("Category","Integration")]
 public async Task Projection_is_idempotent_and_rejects_payload_mutation()
 {
  var connection=Environment.GetEnvironmentVariable("IFM_POSTGRES_CONFIGURATION_TEST_CONNECTION")??"Host=localhost;Port=5432;Database=ifm-configuration-integration-tests";
  var builder=new NpgsqlConnectionStringBuilder(connection);
  if(builder.Database is null||!builder.Database.Contains("test",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("An isolated test database is required.");
  var settings=new DbConnectionSettings().Add(ConfigurationDbContext.ConfigurationDbConnection,connection,"System.Data.Postgres");
  var factory=Substitute.For<IDbContextFactory>();var logger=Substitute.For<ILogger<DbProvider>>();
  var store=new ConfigurationDbContext(settings,factory,logger);factory.ConfigurationDb.Returns(store);
  await new ConfigurationSchemaDb(settings,logger).CreateAllAsync();
  var components=await store.ReadParameterComponentsAsync();
  components.Should().Contain(x=>x.ComponentCode==RegimeDiscoveryParameterModel.ComponentCode&&x.SchemaVersions.SequenceEqual(new[]{1,2,3,4}));
  var schema=await store.ReadParameterSchemaAsync(RegimeDiscoveryParameterModel.ComponentCode,3);
  schema.Should().NotBeNull();schema!.SchemaSha256.Should().Be(ParameterSchemaRegistry.Default.Get(RegimeDiscoveryParameterModel.ComponentCode,3).SchemaSha256);
  var id=Guid.NewGuid();var json=new RegimeDiscoveryParameterModel().CreateDraftPayload(id);
  var version=new ParameterSetVersion(new(id,1,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(json)),"Integration","",2,ParameterVersionStatus.Draft,json,DateTime.UtcNow,"test",CatalogRevision:1);
  var fact=new ParameterSetCreatedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=1,RequestHash=new string('a',64),VersionJson=JsonSerializer.Serialize(version)};
  fact=fact with {AuditJson=JsonSerializer.Serialize(new ParameterAuditEntry(fact.CommandId,id,1,"Create","test",DateTime.UtcNow,"null",JsonSerializer.Serialize(version.Reference)))};
  await store.ProjectParameterSetAsync(fact);await store.ProjectParameterSetAsync(fact);
  (await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,id)).Should().ContainSingle();
  await using(var db=store.CreateConnection().As<NpgsqlConnection>(store.ConnectionString))
  {
   await db.OpenAsync();
   await using(var audit=db.CreateCommand())
   {
    audit.CommandText="SELECT count(*) FROM reference_configuration.parameter_set_audit WHERE operation_id=$1";audit.Parameters.AddWithValue(fact.CommandId);
    Convert.ToInt64(await audit.ExecuteScalarAsync()).Should().Be(1);
   }
   await using(var history=db.CreateCommand())
   {
    history.CommandText="UPDATE reference_configuration.parameter_set_audit SET revision=revision+1 WHERE operation_id=$1";history.Parameters.AddWithValue(fact.CommandId);
    Func<Task> rewrite=async()=>await history.ExecuteNonQueryAsync();
    await rewrite.Should().ThrowAsync<PostgresException>().Where(x=>x.MessageText=="PARAM.HISTORY_IMMUTABLE");
   }
   await using(var schemaMutation=db.CreateCommand())
   {
    schemaMutation.CommandText="UPDATE reference_configuration.parameter_schema_version SET codec='changed' WHERE component_code=$1 AND schema_version=3";schemaMutation.Parameters.AddWithValue(RegimeDiscoveryParameterModel.ComponentCode);
    Func<Task> rewrite=async()=>await schemaMutation.ExecuteNonQueryAsync();
    await rewrite.Should().ThrowAsync<PostgresException>().Where(x=>x.MessageText=="PARAM.SCHEMA_IMMUTABLE");
   }
   await using var mutate=db.CreateCommand();
   mutate.CommandText="UPDATE reference_configuration.parameter_set_version SET body=jsonb_set(body,'{PayloadJson}',to_jsonb('{}'::text)) WHERE set_id=$1";
   mutate.Parameters.AddWithValue(id);
   Func<Task> directMutation=async()=>await mutate.ExecuteNonQueryAsync();
   await directMutation.Should().ThrowAsync<PostgresException>().Where(x=>x.MessageText=="PARAM.VERSION_IMMUTABLE");
  }
  var renamed=version with {Name="Renamed",Description="Metadata only",CatalogRevision=2};
  await store.ProjectParameterSetAsync(new ParameterSetRenamedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=2,RequestHash=new string('c',64),VersionJson=JsonSerializer.Serialize(renamed)});
  var read=(await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,id)).Single();
  read.Name.Should().Be("Renamed");read.CatalogRevision.Should().Be(2);read.PayloadJson.Should().Be(version.PayloadJson);
  var changed=version with {Reference=version.Reference with {PayloadSha256=new string('b',64)}};
  Func<Task> corrupt=()=>store.ProjectParameterSetAsync(fact with {CommandId=Guid.NewGuid(),Revision=3,VersionJson=JsonSerializer.Serialize(changed)});
  await corrupt.Should().ThrowAsync<InvalidOperationException>().WithMessage("PARAM.VERSION_IMMUTABLE");
  (await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,id)).Single().Reference.PayloadSha256.Should().Be(version.Reference.PayloadSha256);
  var published=renamed with {Status=ParameterVersionStatus.Published,PublishedAtUtc=DateTime.UtcNow,CatalogRevision=3};
  await store.ProjectParameterSetAsync(new ParameterVersionPublishedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=3,RequestHash=new string('d',64),VersionJson=JsonSerializer.Serialize(published)});
  var scope=WorkflowParameterScopeModel.Create(TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowDefinition.Id,TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily);
  var assigned=ParameterAssignmentModel.Assign(scope,published,null,0,DateTime.UtcNow,"test");
  // Random isolated projection identity avoids sharing the real workflow scope across test runs.
  assigned=assigned with {AssignmentId=Guid.NewGuid()};
  var assignmentFact=new ParameterAssignmentChangedEvent{CommandId=Guid.NewGuid(),EntityId=new(assigned.AssignmentId),Revision=1,RequestHash=new string('e',64),AssignmentJson=JsonSerializer.Serialize(assigned)};
  await store.ProjectParameterAssignmentAsync(assignmentFact);await store.ProjectParameterAssignmentAsync(assignmentFact);
  var disabled=ParameterAssignmentModel.Disable(assigned,1,DateTime.UtcNow,"test");
  await store.ProjectParameterAssignmentAsync(assignmentFact with {CommandId=Guid.NewGuid(),Revision=2,RequestHash=new string('f',64),AssignmentJson=JsonSerializer.Serialize(disabled)});
  await using(var db=store.CreateConnection().As<NpgsqlConnection>(store.ConnectionString))
  {
   await db.OpenAsync();await using var readAssignment=db.CreateCommand();
   readAssignment.CommandText="SELECT count(*) FROM reference_configuration.parameter_assignment_revision WHERE assignment_id=$1";readAssignment.Parameters.AddWithValue(assigned.AssignmentId);
   Convert.ToInt64(await readAssignment.ExecuteScalarAsync()).Should().Be(2);
  }
  var nextPayload=JsonSerializer.Serialize(RegimeDiscoveryParameterModel.CreateExplicitSeed(id) with {Version=2});
  var nextVersion=published with {SchemaVersion=ParameterSchemaRegistry.CurrentRegimeSchemaVersion,Reference=new(id,2,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(nextPayload)),PayloadJson=nextPayload,Status=ParameterVersionStatus.Draft,PublishedAtUtc=null,CatalogRevision=4};
  await store.ProjectParameterSetAsync(new ParameterDraftSavedEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=4,RequestHash=new string('1',64),VersionJson=JsonSerializer.Serialize(nextVersion)});
  var pageOne=await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,id,limit:1);
  pageOne.Should().ContainSingle().Which.Reference.Version.Should().Be(2);
  pageOne.Single().SchemaVersion.Should().Be(ParameterSchemaRegistry.CurrentRegimeSchemaVersion);
  new RegimeDiscoveryParameterModel().Validate(pageOne.Single().PayloadJson,ParameterSchemaRegistry.CurrentRegimeSchemaVersion).Should().BeEmpty();
  var pageTwo=await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,id,limit:1,afterName:pageOne[0].Name,afterSetId:id,afterVersion:2);
  pageTwo.Should().ContainSingle().Which.Reference.Version.Should().Be(1);
  (await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,id,limit:1,afterName:pageTwo[0].Name,afterSetId:id,afterVersion:1)).Should().BeEmpty();
  var retired=published with {Status=ParameterVersionStatus.Retired,RetiredAtUtc=DateTime.UtcNow,CatalogRevision=5};
  await store.ProjectParameterSetAsync(new ParameterVersionRetiredEvent{CommandId=Guid.NewGuid(),EntityId=new(id),Revision=5,RequestHash=new string('2',64),VersionJson=JsonSerializer.Serialize(retired)});
  (await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,id)).Single(x=>x.Reference.Version==1).Status.Should().Be(ParameterVersionStatus.Retired);
  var runId=Guid.NewGuid();var snapshot=new ParameterStartupSnapshotModel(runId,[],new Dictionary<ParameterVersionRef,ParameterSetVersion>());
  var run=new ParameterStartupRun(runId,[],[],SignalStartupPlanModel.Create(snapshot,SignalStartupPlanModel.ExistingIntradayConsumers()),DateTime.UtcNow,"test");
  var applied=new ParameterStartupChangedEvent{RunId=runId,EntityId=ParameterStartupEntityId.Registry,Revision=1,CommandId=runId,RunJson=JsonSerializer.Serialize(run)};
  await store.ProjectParameterStartupAsync(applied);await store.ProjectParameterStartupAsync(applied);
  var report=new ParameterSignalStartupReport(runId,run.Plan.Fingerprint,new DateOnly(2026,9,10),"ES",DateTime.UtcNow,
   run.Plan.Steps.Select(x=>new ParameterSignalPreparationOutcome(x.Key,ParameterSignalPreparationStatus.ExistingRoute,"Existing route")).ToArray());
  var reportFact=applied with {Revision=2,RunJson=string.Empty,ReportJson=JsonSerializer.Serialize(report)};
  await store.ProjectParameterStartupAsync(reportFact);await store.ProjectParameterStartupAsync(reportFact);
  Func<Task> replaceReport=()=>store.ProjectParameterStartupAsync(reportFact with {ReportJson=JsonSerializer.Serialize(report with {ContractId="CHANGED"})});
  await replaceReport.Should().ThrowAsync<InvalidDataException>().WithMessage("PARAM.STARTUP_REPORT_IMMUTABLE");
  await using(var reportDb=store.CreateConnection().As<NpgsqlConnection>(store.ConnectionString))
  {
   await reportDb.OpenAsync();await using var checkReport=reportDb.CreateCommand();
   checkReport.CommandText="SELECT count(*) FROM reference_configuration.parameter_startup_report WHERE run_id=$1";checkReport.Parameters.AddWithValue(runId);
   Convert.ToInt64(await checkReport.ExecuteScalarAsync()).Should().Be(1);
   checkReport.CommandText="DELETE FROM reference_configuration.parameter_startup_report WHERE run_id=$1";
   Func<Task> deleteReport=async()=>await checkReport.ExecuteNonQueryAsync();
   await deleteReport.Should().ThrowAsync<PostgresException>().Where(x=>x.MessageText=="PARAM.HISTORY_IMMUTABLE");
  }

  await store.ProjectParameterStartupAsync(applied with {Revision=2,Released=true});
  await store.ProjectParameterStartupAsync(applied);
  await using(var db=store.CreateConnection().As<NpgsqlConnection>(store.ConnectionString))
  {
   await db.OpenAsync();await using var check=db.CreateCommand();check.CommandText="SELECT active FROM reference_configuration.parameter_startup_run WHERE run_id=$1";check.Parameters.AddWithValue(runId);
   (await check.ExecuteScalarAsync()).Should().Be(false);
   check.CommandText="UPDATE reference_configuration.parameter_startup_run SET active=true WHERE run_id=$1";
   Func<Task> reactivate=async()=>await check.ExecuteNonQueryAsync();
   await reactivate.Should().ThrowAsync<PostgresException>().Where(x=>x.MessageText=="PARAM.STARTUP_IMMUTABLE");
  }
  var legacyValue=RegimeDiscoveryParameterModel.CreateSeed(Guid.NewGuid());
  await store.InsertRegimeDiscoveryDraftAsync(legacyValue,"Exact legacy fixture","test");
  var legacy=(await store.ReadLegacyParameterVersionsAsync(legacyValue.ParameterSetId,legacyValue.Version)).Single();
  var migratedJson=ParameterLegacyMigrationModel.Expand(legacy);
  var target=ParameterLegacyMigrationModel.TargetId(legacy.Reference);
  var migration=new CreateParameterSetCommand{CommandId=target,EntityId=new(target),Name="Legacy migration fixture",SchemaVersion=2,PayloadJson=migratedJson,LegacySource=legacy.Reference};
  var migratedVersion=ParameterMutationModel.Decide(migration,0,new Dictionary<int,ParameterSetVersion>(),DateTime.UtcNow);
  var migrationFact=new ParameterSetCreatedEvent{CommandId=target,EntityId=new(target),Revision=1,RequestHash=ParameterMutationModel.RequestHash(migration),VersionJson=JsonSerializer.Serialize(migratedVersion)};
  await store.ProjectParameterSetAsync(migrationFact);await store.ProjectParameterSetAsync(migrationFact);
  (await store.ReadParameterSetsAsync(RegimeDiscoveryParameterModel.ComponentCode,target)).Single().LegacySource.Should().Be(legacy.Reference);
  (await store.GetRegimeDiscoveryAsync(legacyValue.ParameterSetId,legacyValue.Version))!.PayloadSha256.Should().Be(legacy.Reference.PayloadSha256);
  await using(var mappingDb=store.CreateConnection().As<NpgsqlConnection>(store.ConnectionString))
  {
   await mappingDb.OpenAsync();await using var mapping=mappingDb.CreateCommand();
   mapping.CommandText="SELECT generic_set_id FROM reference_configuration.parameter_legacy_reference WHERE legacy_kind=$1 AND legacy_set_id=$2 AND legacy_version=$3";
   mapping.Parameters.AddWithValue(legacy.Reference.Kind);mapping.Parameters.AddWithValue(legacy.Reference.SetId);mapping.Parameters.AddWithValue(legacy.Reference.Version);
   (await mapping.ExecuteScalarAsync()).Should().Be(target);
  }
  var first=await store.AcquireParameterWriteLeaseAsync();
  var waiting=store.AcquireParameterWriteLeaseAsync();
  try{await Task.Delay(100);waiting.IsCompleted.Should().BeFalse();}
  finally{await first.DisposeAsync();}
  await using var acquired=await waiting.WaitAsync(TimeSpan.FromSeconds(3));

 }
}
