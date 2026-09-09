using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using FluentAssertions;
using MessagePack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;
[Collection(IntrinsicTimeStrategyWorkflowRuntimeCollection.Name)]
[Trait("Category","Integration")]
public sealed partial class TradeSelectionRuntimeTests(WebApplicationFactory<Program> sourceFactory,TradeDatabaseFixture database):IClassFixture<WebApplicationFactory<Program>>,IClassFixture<TradeDatabaseFixture>
{
    [Fact,Trait("Gate","TS-05"),Trait("Gate","TS-07")]
    public async Task Production_Function_over_NATS_projects_Scylla_and_appends_Postgres_with_idempotent_replay()
    {
        await using var factory=Host();_ = factory.CreateClient();
        var supervisor=factory.Services.GetRequiredService<IActorSupervisor>();
        var producer=factory.Services.GetRequiredService<IActorProducer>();await producer.StartAsync(new(ActorType.Realtime,"TradeSelectionVerification"));
        try
        {
            await factory.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            var c=await TradeSelectionFixture.Command("LongBalancedIronCondor",atUtc:DateTime.UtcNow);
            var first=await producer.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand,TradeSelectionExecutionId,FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(c.Subject,c,c.EntityId);
            first.Success.Should().BeTrue(first.ErrorMessage);first.Value!.IsCompleted.Should().BeTrue(first.Value.Failed?.ErrorMessage);
            var completed=first.Value.Completed!;
            completed.Result.Payload.IsEmpty.Should().BeTrue();
            completed.Result.SelectionResult.Should().NotBeNull();
            var projection=await database.TradeDb.GetTradeSelectionInvocationAsync(c.WorkflowId,c.CommandId);
            projection.Should().NotBeNull();projection!.Result.PayloadSha256.Should().Be(completed.Result.PayloadSha256);
            projection.Result.SelectionResult.Should().NotBeNull();
            await database.TradeDb.UpsertTradeSelectionAsync(projection with { EventId = 123 });
            var container=factory.Services.GetRequiredService<SimpleInjector.Container>();
            var state=await container.GetInstance<IEventSourceFunctionStateRepository<TradeSelectionFunctionState,ExecuteTradeSelectionPipelineCommand>>().LoadStateAsync(c);
            state.IsCompleted.Should().BeTrue();state.Matches(c).Should().BeTrue();
            Func<Task> staleAppend = () => factory.Services.GetRequiredService<IEventSourceActorDbContext>()
                .SaveEventsAsync(c.StreamId, Guid.NewGuid(), new DomainEventCollection([completed with {Id=Guid.NewGuid()}]), 0, CancellationToken.None);
            await staleAppend.Should().ThrowAsync<TomasAI.IFM.Shared.Exceptions.ConcurrencyException>();
            var replay=await producer.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand,TradeSelectionExecutionId,FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(c.Subject,c,c.EntityId);
            replay.Value!.Completed!.Result.PayloadSha256.Should().Be(completed.Result.PayloadSha256);
            var history=await database.TradeDb.GetTradeSelectionHistoryAsync(1,1,DateOnly.FromDateTime(c.EvaluatedAtUtc),200);
            history.Items.Count(x=>x.InvocationId==c.CommandId).Should().Be(1);
            var api=new TradeSelectionQueryApi(producer);
            var queried=await api.GetInvocationAsync(c.WorkflowId,c.CommandId);
            queried.Success.Should().BeTrue(queried.ErrorMessage);queried.Value!.AcceptanceUnknown.Should().BeTrue();queried.Value.WorkflowAccepted.Should().BeFalse();
            (await api.GetResultAsync(c.WorkflowId,c.CommandId,completed.Id)).Value!.ResultId.Should().Be(completed.Id);
            (await api.GetResultAsync(c.WorkflowId,c.CommandId,Guid.NewGuid())).Success.Should().BeFalse();
            var page=await api.GetHistoryAsync(1,1,DateOnly.FromDateTime(c.EvaluatedAtUtc),1);page.Success.Should().BeTrue(page.ErrorMessage);page.Value!.Items.Should().ContainSingle();
            if(page.Value.PagingState is not null)
            {
                var next=await api.GetHistoryAsync(1,1,DateOnly.FromDateTime(c.EvaluatedAtUtc),1,page.Value.PagingState);next.Success.Should().BeTrue(next.ErrorMessage);
                (await api.GetHistoryAsync(1,1,DateOnly.FromDateTime(c.EvaluatedAtUtc).AddDays(1),1,page.Value.PagingState)).Success.Should().BeFalse();
            }
            using(PortfolioAccessScope.Push(new(){Principal="",Roles=[]}))
                (await api.GetInvocationAsync(c.WorkflowId,c.CommandId)).Success.Should().BeFalse();
            var changed=c with{CausationId=Guid.NewGuid()};
            var conflict=await producer.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand,TradeSelectionExecutionId,FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(changed.Subject,changed,changed.EntityId);
            conflict.Value!.Failed!.ReasonCode.Should().Be("TS.CONTRACT.CONFLICTING_DUPLICATE");
        }
        finally{await supervisor.ShutdownAsync();await producer.StopAsync();}
    }
    [Fact,Trait("Gate","TS-02")]
    public async Task Typed_policy_and_activation_use_real_Postgres_immutable_lifecycle()
    {
        await using var factory=Host();_=factory.CreateClient();var supervisor=factory.Services.GetRequiredService<IActorSupervisor>();
        try
        {
            await factory.Services.GetRequiredService<ConfigurationSchemaDb>().CreateAllAsync();
            var db=factory.Services.GetRequiredService<IConfigurationDbContext>();var at=DateTime.UtcNow.AddSeconds(-1);
            var p=TradeSelectionDefaultProfiles.Create(Guid.NewGuid(),Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly);
            await db.InsertTradeSelectionDraftAsync(p,"Trade selection integration fixture","integration");
            var draft=await db.GetTradeSelectionVersionAsync(p.ParameterSetId,p.Version);draft!.PayloadSha256.Should().Be(TradeSelectionPolicy.Hash(p));
            Func<Task> premature=()=>db.ResolveTradeSelectionVersionAsync(p.ParameterSetId,p.Version,draft.PayloadSha256,at);await premature.Should().ThrowAsync<InvalidOperationException>();
            await db.PublishAsync(StrategyParameterSetKind.TradeSelection,p.ParameterSetId,p.Version,at);
            var exact=await db.ResolveTradeSelectionVersionAsync(p.ParameterSetId,p.Version,draft.PayloadSha256,at);exact.Status.Should().Be(ConfigurationParameterSetStatus.Published);
            var activation=new TradeSelectionActivation{SchemaVersion=1,ParameterSetId=Guid.NewGuid(),Version=1,PortfolioId=1,FundId=1,InstrumentRoot="ES",TargetHorizon=p.TargetHorizon,
                SelectionPolicyReference=new(){Kind=CatalogPipelineParameterKind.TradeSelection,Id=p.ParameterSetId,Version=1,PayloadSha256=draft.PayloadSha256}};
            await db.InsertTradeSelectionActivationDraftAsync(activation,"Trade selection integration fixture","integration");
            await db.PublishAsync(StrategyParameterSetKind.IntrinsicTimeStrategyWorkflow,activation.ParameterSetId,1,at);
            var active=await db.ResolveTradeSelectionActivationAsync(activation.ParameterSetId,1,activation.Hash(),at);active.Should().BeEquivalentTo(activation);
            var storage=factory.Services.GetRequiredService<IDbContextFactory>();
            async Task Tamper(string table,Guid id)=>await storage.ConfigurationDb.Use("SelectionVerification.Immutable",$"UPDATE reference_configuration.{table} SET description='changed' WHERE parameter_set_id=$1 AND version=1;")
                .SetParameters(new Values([id])).ExecuteCommandAsync();
            Func<Task> changePolicy=()=>Tamper("trade_selection_parameter_set",p.ParameterSetId);await changePolicy.Should().ThrowAsync<Exception>();
            Func<Task> changeActivation=()=>Tamper("intrinsic_time_strategy_workflow_parameter_set",activation.ParameterSetId);await changeActivation.Should().ThrowAsync<Exception>();
            var construction=new SelectionConstructionPolicy{SchemaVersion=1,ParameterSetId=Guid.NewGuid(),Version=1,MaximumLegs=4,MinimumDaysToExpiry=7,MaximumDaysToExpiry=90,MinimumWingWidth=5.000m,MaximumWingWidth=10m,DeltaUnits="UnderlyingEquivalent",MaximumDeltaTolerance=.10m};
            await db.InsertSelectionConstructionDraftAsync(construction,"Trade selection constraint fixture","integration");
            await db.PublishAsync(StrategyParameterSetKind.OrderComposition,construction.ParameterSetId,1,at);
            var stored=await db.GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.OrderComposition,construction.ParameterSetId,1);TradeSelectionContracts.ValidatePipelinePolicy(stored!);stored!.PayloadSha256.Should().Be(construction.Hash());
            Func<Task> changeConstruction=()=>Tamper("order_composition_parameter_set",construction.ParameterSetId);await changeConstruction.Should().ThrowAsync<Exception>();
            await db.RetireAsync(StrategyParameterSetKind.OrderComposition,construction.ParameterSetId,1,at.AddMilliseconds(1));
            await db.RetireAsync(StrategyParameterSetKind.IntrinsicTimeStrategyWorkflow,activation.ParameterSetId,1,at.AddMilliseconds(1));
            await db.RetireAsync(StrategyParameterSetKind.TradeSelection,p.ParameterSetId,1,at.AddMilliseconds(1));
            Func<Task> retired=()=>db.ResolveTradeSelectionVersionAsync(p.ParameterSetId,p.Version,draft.PayloadSha256,DateTime.UtcNow);await retired.Should().ThrowAsync<InvalidOperationException>();
        }
        finally{await supervisor.ShutdownAsync();}
    }
    [Fact,Trait("Gate","TS-03")]
    public async Task Real_Scylla_assignment_paging_crosses_history_and_returns_seventeen_row_overflow_sentinel()
    {
        var settings=new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add("PortfolioDbConnection","Contact Points=localhost;Port=9042;Default Keyspace=trade_test_db","System.Data.ScyllaDb");
        var logger=Substitute.For<Microsoft.Extensions.Logging.ILogger<TomasAI.IFM.Framework.Storage.DbProvider>>();
        await new TomasAI.IFM.Application.Storage.PortfolioDb.Schema.PortfolioSchemaDb(settings,logger).CreateAllAsync();
        var repositories=new Dictionary<Type,object>();var factory=new DbContextFactory(new DbContextResolver(t=>repositories[t]));
        var db=new TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbContext(settings,factory,logger);
        repositories.Add(typeof(TomasAI.IFM.Framework.Storage.IObjectRepository<TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbContext>),db);
        var c=await TradeSelectionFixture.Command(atUtc:DateTime.UtcNow);var a=c.SelectionBinding.PortfolioSnapshot.Assignments.Single();
        var id=Random.Shared.Next(1000000,2000000);var now=c.EvaluatedAtUtc;
        try
        {
            for(var n=0;n<86;n++)
            {
                var row=a with {PortfolioId=id,FundId=id,TradeTemplateId=Guid.NewGuid(),AssignmentVersion=n+1,Enabled=n%2==0,AssetType=n%2==0?"Futures":"FuturesOptions",EffectiveUntilUtc=n<70?now.AddSeconds(-1):null};
                await db.UpsertAssignmentAsync(TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioProjection<FundTradeTemplateAssignmentReadModel>.Create(row,n+1,3000000+n,now));
            }
            var rows=await db.GetSelectionAssignmentsAsync(id,id,1,"Daily","ES",now);rows.Should().HaveCount(16);rows.Should().Contain(x=>!x.Enabled && x.AssetType=="FuturesOptions");
            var extra=a with {PortfolioId=id,FundId=id,TradeTemplateId=Guid.NewGuid(),AssignmentVersion=87};
            await db.UpsertAssignmentAsync(TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioProjection<FundTradeTemplateAssignmentReadModel>.Create(extra,87,3000087,now));
            (await db.GetSelectionAssignmentsAsync(id,id,1,"Daily","ES",now)).Should().HaveCount(17);
        }
        finally
        {
            await factory.PortfolioDb.Use("SelectionVerification.CleanupAssignments","DELETE FROM fund_template_assignment WHERE portfolioId=? AND fundId=? AND fundMandateVersion=?;").SetParameters(new Values([id,id,1L])).ExecuteCommandAsync();
        }
    }
    [Fact,Trait("Gate","TS-05"),Trait("Gate","TS-08")]
    public async Task Actual_Scylla_orphan_after_append_failure_is_recovered_by_the_identical_Function_request()
    {
        var recorder=new FailingRepository();
        await using var factory=Host(services=>
        {
            var container=(SimpleInjector.Container)services.Single(x=>x.ServiceType==typeof(SimpleInjector.Container)).ImplementationInstance!;
            container.Register<TradeSelectionFunctionStateRepository>();recorder.Resolve=()=>container.GetInstance<TradeSelectionFunctionStateRepository>();
            container.RegisterInstance<IEventSourceFunctionStateRepository<TradeSelectionFunctionState,ExecuteTradeSelectionPipelineCommand>>(recorder);
        });_=factory.CreateClient();var supervisor=factory.Services.GetRequiredService<IActorSupervisor>();
        var producer=factory.Services.GetRequiredService<IActorProducer>();await producer.StartAsync(new(ActorType.Realtime,"SelectionCrashVerification"));
        try
        {
            await factory.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            var c=await TradeSelectionFixture.Command(atUtc:DateTime.UtcNow);
            var first=await producer.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand,TradeSelectionExecutionId,FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(c.Subject,c,c.EntityId);
            first.Value!.IsFailed.Should().BeTrue();first.Value.Failed!.ReasonCode.Should().Be("TS.PERSISTENCE.FAILED");
            (await database.TradeDb.GetTradeSelectionInvocationAsync(c.WorkflowId,c.CommandId)).Should().NotBeNull("projection precedes completed append");
            (await recorder.Resolve().LoadStateAsync(c)).IsCompleted.Should().BeFalse();
            recorder.Fail=false;
            var retry=await producer.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand,TradeSelectionExecutionId,FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(c.Subject,c,c.EntityId);
            retry.Value!.IsCompleted.Should().BeTrue(retry.Value.Failed?.ErrorMessage);(await recorder.Resolve().LoadStateAsync(c)).IsCompleted.Should().BeTrue();
        }
        finally{await supervisor.ShutdownAsync();await producer.StopAsync();}
    }
    sealed class FailingRepository:IEventSourceFunctionStateRepository<TradeSelectionFunctionState,ExecuteTradeSelectionPipelineCommand>
    {
        public bool Fail=true;public Func<TradeSelectionFunctionStateRepository> Resolve=null!;
        public ValueTask<TradeSelectionFunctionState> LoadStateAsync(ExecuteTradeSelectionPipelineCommand c,CancellationToken t=default)=>Resolve().LoadStateAsync(c,t);
        public ValueTask SaveCompletedStateAsync(IFunctionActorContext context,TradeSelectionFunctionState state,ExecuteTradeSelectionPipelineCommand c,CancellationToken t=default)
            =>Fail?ValueTask.FromException(new InvalidOperationException("Injected completed append failure")):Resolve().SaveCompletedStateAsync(context,state,c,t);
    }
    WebApplicationFactory<Program> Host(Action<IServiceCollection>? configure=null,string? brokerUrl=null)=>sourceFactory.WithWebHostBuilder(builder=>builder.UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.MarketData.Analytics")
        .UseSetting("IFM_TEST_NATS_URL",brokerUrl??"nats://127.0.0.1:14222").ConfigureServices(services=>
        {
            services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions{Enabled=false});
            var validators=TradeSelectionCatalogCapabilities.Create().Concat(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionCatalogCapabilities.Create())
                .Concat(TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model.RiskCatalogCapabilities.Create());
            var registry=new StrategyCatalogCapabilityRegistry(validators);services.RemoveAll<IStrategyCatalogCapabilities>();services.AddSingleton<IStrategyCatalogCapabilities>(registry);
            var container=(SimpleInjector.Container)services.Single(x=>x.ServiceType==typeof(SimpleInjector.Container)).ImplementationInstance!;
            var authority=Substitute.For<IPortfolioQueryApi>();
            authority.GetFundAsync(1,1,Arg.Any<long?>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundMandateReadModel>(new(){PortfolioId=1,FundId=1}));
            services.RemoveAll<IPortfolioQueryApi>();services.AddSingleton(authority);
            container.Options.AllowOverridingRegistrations=true;container.RegisterInstance<IPortfolioQueryApi>(authority);container.RegisterInstance<IStrategyCatalogCapabilities>(registry);configure?.Invoke(services);
        }));
    readonly record struct Values(object[] Items):TomasAI.IFM.Framework.Storage.IBindValue {public object Bind()=>Items;}
}
