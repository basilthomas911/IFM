using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.LogDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.IntegrationTests.ParameterSets;
/// <summary>Requires disposable brokers on 24222/26379 and PostgreSQL on 25432; never uses default application infrastructure.</summary>
public sealed class ParameterSetActorRuntimeTests
{
 [Fact]public async Task Real_actor_routes_complete_parameter_lifecycle_and_keep_frozen_startup_versions()
 {
  const string connection="Host=127.0.0.1;Port=25432;Database=ifm-parametersets-runtime-tests";
  var settings=new DbConnectionSettings().Add("ConfigurationDbConnection",connection,"System.Data.Postgres")
   .Add("EventSourceActorDbConnection",connection,"System.Data.Postgres").Add("LogDbConnection",connection,"System.Data.Postgres").Add("SequenceIdDbConnection",connection,"System.Data.Postgres");
  var logger=NullLogger<DbProvider>.Instance;
  await new ConfigurationSchemaDb(settings,logger).CreateAllAsync();
  await new EventSourceSchemaDb(settings,logger).CreateAllAsync();
  await new LogSchemaDb(settings,logger).CreateAllAsync();
  await new SequenceIdSchemaDb(settings,logger).CreateAllAsync();
  await using var source=new WebApplicationFactory<Program>();
  await using var host=source.WithWebHostBuilder(builder=>builder.UseEnvironment("Development")
   .UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Reference")
   .UseSetting("IFM_TEST_NATS_URL","nats://127.0.0.1:24222")
   .UseSetting("IFM_TEST_REDIS_URL","127.0.0.1:26379")
   .UseSetting("IFM_TEST_POSTGRES_CONNECTION",connection)
   .UseSetting("ConnectionStrings:ConfigurationDbConnection",connection)
   .UseSetting("ConnectionStrings:EventSourceActorDbConnection",connection)
   .UseSetting("ConnectionStrings:LogDbConnection",connection)
   .UseSetting("ConnectionStrings:SequenceIdDbConnection",connection)
   .ConfigureServices(services=>services.AddSingleton<IParameterAccessPolicy>(new SingleUserDevelopmentParameterAccessPolicy("Development",true))));
  using var client=host.CreateClient();
  var producer=host.Services.GetRequiredService<IActorProducer>();
  await producer.StartAsync(new ActorMailboxId(ActorType.Query,"ParameterSetsAcceptance"));
  try
  {
   var api=new ParameterSetsApi(producer);using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));var token=deadline.Token;
   var id=Guid.NewGuid();var draft=await api.PreviewAsync(id,token);Assert.True(draft.Success,draft.ErrorMessage);
   var create=new CreateParameterSetCommand{CommandId=Guid.NewGuid(),EntityId=new(id),Name="Runtime acceptance",SchemaVersion=ParameterSchemaRegistry.CurrentRegimeSchemaVersion,PayloadJson=draft.Value!};
   var created=await api.CreateAsync(create,token);Assert.True(created.Success,created.ErrorMessage);
   var repeated=await api.CreateAsync(create,token);Assert.True(repeated.Success,repeated.ErrorMessage);
   var saved=await api.StateAsync(id,token);Assert.True(saved.Success,saved.ErrorMessage);Assert.Equal(1,saved.Value!.Revision);
   var published=await api.PublishAsync(new(){CommandId=Guid.NewGuid(),EntityId=new(id),Version=1,ExpectedRevision=1},token);Assert.True(published.Success,published.ErrorMessage);
   var version=(await api.StateAsync(id,token)).Value!.Versions.Single();
   var scope=WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,TimeFrameType.Daily);
   var assignmentBefore=await api.AssignmentAsync(IntrinsicTimeStrategyWorkflowDefinition.Id,(int)TimeFrameType.Daily,token);
   var assignmentRevision=assignmentBefore.Value?.Revision??0;
   var assign=await api.AssignAsync(new(){CommandId=Guid.NewGuid(),EntityId=new(WorkflowParameterScopeModel.AssignmentId(scope)),Scope=scope,Reference=version.Reference,ExpectedRevision=assignmentRevision},token);Assert.True(assign.Success,assign.ErrorMessage);
   var runId=Guid.NewGuid();var apply=await api.ApplyStartupAsync(new(){CommandId=runId,RunId=runId},token);Assert.True(apply.Success,apply.ErrorMessage);
   var runs=await api.StartupRunsAsync(token);Assert.True(runs.Success,runs.ErrorMessage);var run=runs.Value!.Single(x=>x.RunId==runId);
   var report=new ParameterSignalStartupReport(runId,run.Plan.Fingerprint,new DateOnly(2026,9,10),"ES-PARAMETER-TEST",DateTime.UtcNow,
    run.Plan.Steps.Select(x=>new ParameterSignalPreparationOutcome(x.Key,x.Prepare?ParameterSignalPreparationStatus.ExistingRoute:ParameterSignalPreparationStatus.NotRequested,"Acceptance fixture; no live producers started.")).ToArray());
   var record=new RecordSignalStartupReportCommand{CommandId=Guid.NewGuid(),RunId=runId,Report=report};
   var recorded=await api.RecordStartupReportAsync(record,token);Assert.True(recorded.Success,recorded.ErrorMessage);
   var reportRetry=await api.RecordStartupReportAsync(record,token);Assert.True(reportRetry.Success,reportRetry.ErrorMessage);
   var readReport=await api.StartupReportAsync(runId,token);Assert.True(readReport.Success,readReport.ErrorMessage);Assert.Equal(report.Outcomes.Length,readReport.Value!.Outcomes.Length);
   var monitor=await api.SignalMonitoringAsync(runId,token);Assert.True(monitor.Success,monitor.ErrorMessage);Assert.NotEmpty(monitor.Value!.Rows);
   Assert.All(monitor.Value.Rows,x=>Assert.Equal(TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery.RegimeDiscoverySignalAvailability.Missing,x.Observation.Availability));
   var inUse=await api.RetireAsync(new(){CommandId=Guid.NewGuid(),EntityId=new(id),Version=1,ExpectedRevision=2},token);Assert.False(inUse.Success);Assert.Contains("PARAM.VERSION_IN_USE",inUse.ErrorMessage);
   var assignment=await api.AssignmentAsync(IntrinsicTimeStrategyWorkflowDefinition.Id,(int)TimeFrameType.Daily,token);
   var disabled=await api.DisableAssignmentAsync(new(){CommandId=Guid.NewGuid(),EntityId=new(WorkflowParameterScopeModel.AssignmentId(scope)),Scope=scope,Reference=assignment.Value!.Assignment!.Reference,ExpectedRevision=assignment.Value.Revision},token);Assert.True(disabled.Success,disabled.ErrorMessage);
   var retired=await api.RetireAsync(new(){CommandId=Guid.NewGuid(),EntityId=new(id),Version=1,ExpectedRevision=2},token);Assert.True(retired.Success,retired.ErrorMessage);
   var retained=(await api.StartupRunsAsync(token)).Value!.Single(x=>x.RunId==runId).Versions.Single();Assert.Equal(ParameterVersionStatus.Published,retained.Status);
  }
  finally{await producer.StopAsync();}
 }
}
