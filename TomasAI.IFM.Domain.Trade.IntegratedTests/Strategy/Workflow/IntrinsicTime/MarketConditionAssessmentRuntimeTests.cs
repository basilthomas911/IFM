using FluentAssertions;
using MessagePack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests
{
    [Theory]
    [InlineData("capture")][InlineData("project")][InlineData("append")][InlineData("timeout")]
    [Trait("Gate","MC-R08")]
    public async Task Assessment_runtime_failure_never_advances_and_reports_unaccepted_projection(string failure)
    {
        var profile="MC-R08-Fault-"+Guid.NewGuid().ToString("N");
        var source=new FaultingAssessmentSource(failure);
        await using var factory=sourceFactory.WithWebHostBuilder(builder=>builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.MarketData.Analytics")
            .UseSetting("IFM_TEST_NATS_URL","nats://127.0.0.1:14222").ConfigureServices(services=>
            {
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled=true,MarketConditionAssessmentProfileId=profile });
                services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>();services.AddSingleton<IMarketConditionAssessmentSnapshotProvider>(source);
                if(failure=="project")OverrideSimpleInjector<IFunctionProjector<MarketConditionAssessmentCompletedEvent>>(services,new FailingAssessmentProjector());
                if(failure=="append")
                {
                    var container=(SimpleInjector.Container)services.Single(x=>x.ServiceType==typeof(SimpleInjector.Container)).ImplementationInstance!;
                container.Register<MarketConditionAssessmentStateRepository>();
                    OverrideSimpleInjector<IEventSourceFunctionStateRepository<MarketConditionAssessmentState,ExecuteMarketConditionAssessmentCommand>>(services,
                        new RecordingAssessmentRepository {FailPersist=true,Resolve=()=>container.GetInstance<MarketConditionAssessmentStateRepository>()});
                }
            }));
        _=factory.CreateClient();var supervisor=factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines=await DummyPipelineHarness.StartAsync(factory.Services,supervisor);
        var publisher=factory.Services.GetRequiredService<IActorProducer>();await publisher.StartAsync(new(ActorType.Realtime,"AssessmentFault"));
        try
        {
            var entity=Entity("ES-MCFAULT-"+Guid.NewGuid().ToString("N")[..8],TimeFrameType.Daily);
            await PrepareRegimeDiscoveryAsync(factory.Services,[entity],profile);await PublishTriggerAsync(publisher,entity.ItiSignalEntityId);
            var terminal=await WaitForTerminalAsync(entity,StrategyWorkflowStatus.Stopped,failure=="timeout"?StrategyWorkflowOutcome.TimedOut:StrategyWorkflowOutcome.PipelineFailed);
            if(failure=="timeout") { source.Release.TrySetResult();await source.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));await Task.Delay(100); }
            var view=(await LoadStateAsync(factory.Services,entity)).CurrentView!;
            view.MarketCondition.Result.Should().BeNull();pipelines.StartCount(entity,StrategyWorkflowStage.TradeSelection).Should().Be(0);
            var projected=await database.TradeDb.GetMarketConditionAssessmentAsync(terminal.WorkflowId);
            if(failure=="append")projected.Should().NotBeNull();else projected.Should().BeNull();
            var observed=await new IntrinsicTimeStrategyWorkflowQueryApi(publisher).GetObservationAsync(entity);
            observed.Success.Should().BeTrue(observed.ErrorMessage);observed.Value!.WorkflowAcceptedMarketAssessment.Should().BeFalse();
            observed.Value.MarketAssessmentOrphanSuspected.Should().Be(failure=="append");
        }
        finally{source.Release.TrySetResult();await pipelines.DisposeAsync();await supervisor.ShutdownAsync();await publisher.StopAsync();}
    }

    sealed class FailingAssessmentProjector:IFunctionProjector<MarketConditionAssessmentCompletedEvent>
    {
        public ValueTask ProjectAsync(MarketConditionAssessmentCompletedEvent completed,CancellationToken cancellationToken=default)
            =>ValueTask.FromException(new InvalidOperationException("Injected assessment projection failure"));
    }
    sealed class FaultingAssessmentSource(string failure):IMarketConditionAssessmentSnapshotProvider
    {
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Returned {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<MarketConditionAssessmentSnapshot> CaptureAsync(MarketConditionAssessmentParameterSet p,DateTime at,CancellationToken ct)
        {
            if(failure=="capture")throw new InvalidOperationException("Injected source failure");
            if(failure=="timeout")await Release.Task; // Intentionally ignores cancellation to verify the late-worker fence.
            var snapshot=await new AssessmentSourceFixture().CaptureAsync(p,at,default);Returned.TrySetResult();return snapshot;
        }
    }
    [Fact]
    [Trait("Gate","MC-R09")]
    public async Task Assessment_restart_with_new_starts_disabled_preserves_completed_authority()
    {
        var profile="MC-R09-"+Guid.NewGuid().ToString("N");
        var entity=Entity("ES-MCR09-"+Guid.NewGuid().ToString("N")[..8],TimeFrameType.Weekly);
        var source=new AssessmentSourceFixture(); var recorder=new RecordingAssessmentRepository();
        var firstOptions=new IntrinsicTimeStrategyWorkflowOptions {Enabled=true,MarketConditionAssessmentProfileId=profile};
        ExecuteMarketConditionAssessmentCommand command;
        byte[] acceptedPayload;
        await using(var first=sourceFactory.WithWebHostBuilder(builder=>builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.MarketData.Analytics")
            .UseSetting("IFM_TEST_NATS_URL","nats://127.0.0.1:14222").ConfigureServices(services=>
            {
                services.AddSingleton(firstOptions);
                services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>();services.AddSingleton<IMarketConditionAssessmentSnapshotProvider>(source);
                var container=(SimpleInjector.Container)services.Single(x=>x.ServiceType==typeof(SimpleInjector.Container)).ImplementationInstance!;
                container.Register<MarketConditionAssessmentStateRepository>();
                recorder.Resolve=()=>container.GetInstance<MarketConditionAssessmentStateRepository>();
                OverrideSimpleInjector<IEventSourceFunctionStateRepository<MarketConditionAssessmentState,ExecuteMarketConditionAssessmentCommand>>(services,recorder);
            })))
        {
            _=first.CreateClient(); var supervisor=first.Services.GetRequiredService<IActorSupervisor>();
            await using var pipelines=await DummyPipelineHarness.StartAsync(first.Services,supervisor);
            var producer=first.Services.GetRequiredService<IActorProducer>();await producer.StartAsync(new(ActorType.Realtime,"AssessmentRestart"));
            try
            {
                await PrepareRegimeDiscoveryAsync(first.Services,[entity],profile);
                using var hold=pipelines.HoldAt(entity,StrategyWorkflowStage.TradeSelection);
                await PublishTriggerAsync(producer,entity.ItiSignalEntityId);
                await WaitForStageAsync(first.Services,entity,StrategyWorkflowStage.TradeSelection,3);
                await pipelines.WaitForStartCountAsync(entity,StrategyWorkflowStage.TradeSelection,1);
                command=recorder.Command!; command.Should().NotBeNull();
                var original=await producer.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand,MarketConditionAssessmentExecutionId,
                    FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>>(command.Subject,command,command.EntityId);
                original.Success.Should().BeTrue(original.ErrorMessage);acceptedPayload=original.Value!.Completed!.Result.Payload.ToArray();
                source.Calls[TimeFrameType.Weekly].Should().Be(1);

            }
            finally{await pipelines.DisposeAsync();await supervisor.ShutdownAsync();await producer.StopAsync();}
        }
        var afterRestart=new AssessmentSourceFixture();
        await using var restarted=sourceFactory.WithWebHostBuilder(builder=>builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.MarketData.Analytics")
            .UseSetting("IFM_TEST_NATS_URL","nats://127.0.0.1:14222").ConfigureServices(services=>
            {
                // Disabling new starts does not invalidate persisted assessment authority.
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled=false });
                services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>();services.AddSingleton<IMarketConditionAssessmentSnapshotProvider>(afterRestart);
            }));
        _=restarted.CreateClient(); var supervisor2=restarted.Services.GetRequiredService<IActorSupervisor>();
        await using var later=await DummyPipelineHarness.StartAsync(restarted.Services,supervisor2);
        var publisher=restarted.Services.GetRequiredService<IActorProducer>();await publisher.StartAsync(new(ActorType.Realtime,"AssessmentRollback"));
        try
        {
            var retry=await publisher.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand,MarketConditionAssessmentExecutionId,
                FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>>(command.Subject,command,command.EntityId);
            retry.Success.Should().BeTrue(retry.ErrorMessage);retry.Value!.Completed!.Result.Payload.ToArray().Should().Equal(acceptedPayload);
            afterRestart.Calls.Should().BeEmpty();
            var conflict=command with {CorrelationId=Guid.NewGuid()};
            var rejected=await publisher.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand,MarketConditionAssessmentExecutionId,
                FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>>(conflict.Subject,conflict,conflict.EntityId);
            rejected.Value!.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.ContractInvalid);
            var restored=(await LoadStateAsync(restarted.Services,entity)).CurrentView!;
            restored.AssessmentBinding!.Parameters.MarketProfileId.Should().Be(profile);restored.WorkflowRevision.Should().Be(3);
            later.StartCount(entity,StrategyWorkflowStage.TradeSelection).Should().Be(0);
            var nextEntity=Entity("ES-MCR09-DISABLED-"+Guid.NewGuid().ToString("N")[..8],TimeFrameType.Daily);
            await PublishTriggerAsync(publisher,nextEntity.ItiSignalEntityId);
            await Task.Delay(250);
            (await LoadStateAsync(restarted.Services,nextEntity)).CurrentView.Should().BeNull();
            afterRestart.Calls.Should().BeEmpty();
            (await new MarketConditionAssessmentQueryApi(publisher).GetAsync(restored.WorkflowId)).Value!.Result.Payload.ToArray().Should().Equal(acceptedPayload);
        }
        finally{await later.DisposeAsync();await supervisor2.ShutdownAsync();await publisher.StopAsync();}
    }

    sealed class RecordingAssessmentRepository:IEventSourceFunctionStateRepository<MarketConditionAssessmentState,ExecuteMarketConditionAssessmentCommand>
    {
        public Func<MarketConditionAssessmentStateRepository> Resolve {get;set;}=null!;
        public ExecuteMarketConditionAssessmentCommand? Command {get;private set;}
        public bool FailPersist {get;init;}
        public ValueTask<MarketConditionAssessmentState> LoadStateAsync(ExecuteMarketConditionAssessmentCommand command,CancellationToken ct=default)
        { Command??=command;return Resolve().LoadStateAsync(command,ct); }
        public ValueTask SaveCompletedStateAsync(IFunctionActorContext context,MarketConditionAssessmentState state,ExecuteMarketConditionAssessmentCommand command,CancellationToken ct=default)
            =>FailPersist?ValueTask.FromException(new InvalidOperationException("Injected assessment append failure")):Resolve().SaveCompletedStateAsync(context,state,command,ct);
    }
    [Fact]
    [Trait("Gate","MC-R08")]
    public async Task Assessment_three_horizon_workflows_use_real_upstream_function_storage_and_observation()
    {
        var profile="MC-R08-"+Guid.NewGuid().ToString("N");
        var provider=new AssessmentSourceFixture {OptionalUnavailable=true,Poor=true};
        await using var factory=sourceFactory.WithWebHostBuilder(builder=>builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.MarketData.Analytics").UseSetting("IFM_TEST_NATS_URL","nats://127.0.0.1:14222")
            .ConfigureAppConfiguration((_,configuration)=>configuration.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["AppSettings:IntrinsicTimeStrategyWorkflow:Enabled"]="true",
                ["AppSettings:IntrinsicTimeStrategyWorkflow:MarketConditionAssessmentProfileId"]=profile
            }))
            .ConfigureServices(services=>
            {
                services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>(); services.AddSingleton<IMarketConditionAssessmentSnapshotProvider>(provider);
            }));
        _=factory.CreateClient();
        var supervisor=factory.Services.GetRequiredService<IActorSupervisor>(); supervisor.IsReady.Should().BeTrue();
        await using var pipelines=await DummyPipelineHarness.StartAsync(factory.Services,supervisor);
        var publisher=factory.Services.GetRequiredService<IActorProducer>();
        await publisher.StartAsync(new(ActorType.Realtime,"AssessmentQualification"));
        try
        {
            // Each start resolves only its own profile and accepted upstream result. No bundled timeframe requirement.
            foreach(var horizon in new[]{TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly})
            {
                var entity=Entity("ES-MCR08-"+Guid.NewGuid().ToString("N")[..8],horizon);
                await PrepareRegimeDiscoveryAsync(factory.Services,[entity],profile);
                var hold=pipelines.HoldAt(entity,StrategyWorkflowStage.TradeSelection);
                await PublishTriggerAsync(publisher,entity.ItiSignalEntityId);
                var advanced=await WaitForStageAsync(factory.Services,entity,StrategyWorkflowStage.TradeSelection,3);
                await pipelines.WaitForStartCountAsync(entity,StrategyWorkflowStage.TradeSelection,1);
                var state=(await LoadStateAsync(factory.Services,entity)).CurrentView!;
                state.AssessmentBinding!.Parameters.TargetHorizon.Should().Be(horizon);
                var observed=await new IntrinsicTimeStrategyWorkflowQueryApi(publisher).GetObservationAsync(entity);
                observed.Success.Should().BeTrue(observed.ErrorMessage);
                observed.Value!.WorkflowAcceptedMarketAssessment.Should().BeTrue(); observed.Value.MarketAssessmentOrphanSuspected.Should().BeFalse();
                observed.Value.MarketAssessment!.TargetHorizon.Should().Be(horizon);
                observed.Value.MarketAssessment.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
                observed.Value.MarketAssessment.Assessment.LiquidityCondition.Should().Be(AssessmentLiquidity.Poor);
                observed.Value.MarketAssessment.Assessment.StressState.Should().Be(AssessmentStress.Unknown);
                var queries=new MarketConditionAssessmentQueryApi(publisher);
                var exact=await queries.GetAsync(advanced.WorkflowId); exact.Success.Should().BeTrue(exact.ErrorMessage);
                exact.Value!.Result.Payload.ToArray().Should().Equal(state.MarketCondition.Result!.Payload.ToArray());
                var latest=await queries.LatestAsync(profile,"ES",horizon); latest.Success.Should().BeTrue(latest.ErrorMessage);
                latest.Value.Should().ContainSingle(x=>x.WorkflowId==advanced.WorkflowId);
                var restored=MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(MessagePackSerializer.Serialize(state));
                restored.AssessmentBinding.PayloadSha256.Should().Be(state.AssessmentBinding.PayloadSha256);
                provider.Calls[horizon].Should().Be(1); pipelines.StartCount(entity,StrategyWorkflowStage.TradeSelection).Should().Be(1);
                var evidence=Environment.GetEnvironmentVariable("IFM_MC_EVIDENCE_DIR")??Path.Combine(Directory.GetCurrentDirectory(),".codex-mc-evidence");Directory.CreateDirectory(evidence);
                await File.WriteAllBytesAsync(Path.Combine(evidence,horizon+".workflow.msgpack"),MessagePackSerializer.Serialize(state));
                await File.WriteAllBytesAsync(Path.Combine(evidence,horizon+".assessment.msgpack"),MessagePackSerializer.Serialize(exact.Value));
                hold.Release();
            }
            var refSubject=new ActorSubject(ActorType.Query,GetMarketConditionAssessmentReferenceQuery.Actor,GetMarketConditionAssessmentReferenceQuery.Verb,"assessment-reference");
            var reference=await publisher.RequestAsync<MarketConditionAssessmentReferenceRow[],GetMarketConditionAssessmentReferenceQuery>(refSubject,new(){Subject=refSubject,EntityId=new ActorEntityId("assessment-reference")});
            reference.Success.Should().BeTrue(reference.ErrorMessage); reference.Value.Should().HaveCount(30);
            var output=Path.Combine(Path.GetTempPath(),"mc-assessment-reference-"+Guid.NewGuid().ToString("N")+".csv");
            await new Shared.DataExport.MarketConditionAssessmentCsvAdapter().ExportAsync(reference.Value!,output);
            var csv=await File.ReadAllTextAsync(output); csv.Should().Contain("TargetHorizon").And.NotContain("Tradeability").And.NotContain("HintTradeType");
            File.Delete(output);
        }
        finally { await publisher.StopAsync(); }
    }

    [Fact]
    [Trait("Gate","MC-R08")]
    public async Task Assessment_known_unavailability_is_completed_no_trade_without_selector_dispatch()
    {
        var profile="MC-R08-Missing-"+Guid.NewGuid().ToString("N");
        var provider=new AssessmentSourceFixture { Unavailable=true };
        await using var factory=sourceFactory.WithWebHostBuilder(builder=>builder.UseSetting("IFM_TEST_ACTOR_DOMAIN","TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.MarketData.Analytics")
            .UseSetting("IFM_TEST_NATS_URL","nats://127.0.0.1:14222").ConfigureServices(services=>
            {
                services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled=true,MarketConditionAssessmentProfileId=profile });
                services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>(); services.AddSingleton<IMarketConditionAssessmentSnapshotProvider>(provider);
            }));
        _=factory.CreateClient();
        var supervisor=factory.Services.GetRequiredService<IActorSupervisor>();
        await using var pipelines=await DummyPipelineHarness.StartAsync(factory.Services,supervisor);
        var publisher=factory.Services.GetRequiredService<IActorProducer>(); await publisher.StartAsync(new(ActorType.Realtime,"AssessmentMissingQualification"));
        try
        {
            var entity=Entity("ES-MCR08-"+Guid.NewGuid().ToString("N")[..8],TimeFrameType.Weekly);
            await PrepareRegimeDiscoveryAsync(factory.Services,[entity],profile);
            await PublishTriggerAsync(publisher,entity.ItiSignalEntityId);
            var terminal=await WaitForTerminalAsync(entity,StrategyWorkflowStatus.Completed,StrategyWorkflowOutcome.NoTrade);
            var projected=await database.TradeDb.GetMarketConditionAssessmentAsync(terminal.WorkflowId);
            MarketConditionAssessmentContracts.ReadResult(projected!.Result).Assessment.Availability.Should().Be(AssessmentAvailability.Unavailable);
            pipelines.StartCount(entity,StrategyWorkflowStage.TradeSelection).Should().Be(0);
        }
        finally { await publisher.StopAsync(); }
    }

    sealed class AssessmentSourceFixture:IMarketConditionAssessmentSnapshotProvider
    {
        public bool Unavailable { get; init; }
        public bool OptionalUnavailable { get; init; }
        public bool Poor { get; init; }
        public Dictionary<TimeFrameType,int> Calls { get; }=[];
        public ValueTask<MarketConditionAssessmentSnapshot> CaptureAsync(MarketConditionAssessmentParameterSet p,DateTime at,CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls[p.TargetHorizon]=Calls.GetValueOrDefault(p.TargetHorizon)+1;
            return ValueTask.FromResult(new MarketConditionAssessmentSnapshot
            {
                SnapshotId=Guid.NewGuid(),MarketProfileId=p.MarketProfileId,InstrumentRoot=p.InstrumentRoot,TargetHorizon=p.TargetHorizon,ReferenceInstrumentId="ES-Qualification",
                EvaluatedAtUtc=at,Quote=Poor?new(5000,5002,1,1):new(5000,5000.25m,10,10),SessionState=Poor?MarketSessionStatus.Closed:MarketSessionStatus.Open,EventContext=Poor?AssessmentEventContext.Elevated:AssessmentEventContext.Clear,
                Observations=p.Sources.Select(x=>new AssessmentObservation { SourceId=x.SourceId,ObservedAtUtc=at,ReceivedAtUtc=at,Sequence=10,Value=0m,
                    Availability=Unavailable&&x.Required||OptionalUnavailable&&!x.Required?MarketSourceAvailability.Unavailable:MarketSourceAvailability.Available,Validity=MarketSourceValidity.Valid }).ToArray(),
                CalendarEvidence=new(){CheckedAtUtc=at,CoverageConfirmed=!Unavailable,ValidUntilUtc=Unavailable?null:at.AddHours(1),Reason="Controlled source fixture"}
            }.Seal());
        }
    }
}
