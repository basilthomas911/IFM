using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Fact,Trait("Category","PortfolioFinancialRuntime"),Trait("Gate","PF-FIN-05")]
    public async Task Risk_profiles_publish_exact_horizon_versions_in_real_configuration_storage()
    {
        var broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")??throw new InvalidOperationException("An isolated test broker is required.");
        await using var host=Host(brokerUrl:broker);_=host.CreateClient();var supervisor=host.Services.GetRequiredService<IActorSupervisor>();
        try
        {
            await host.Services.GetRequiredService<TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb>().CreateAllAsync();
            var db=host.Services.GetRequiredService<TomasAI.IFM.Application.Storage.ConfigurationDb.IConfigurationDbContext>();
            foreach(var horizon in new[] { TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly,TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly })
            {
                var policy=RiskParameterSet.Default(horizon) with { ParameterSetId=Guid.NewGuid() };
                await db.InsertRiskManagementDraftAsync(policy,"Isolated Risk publication qualification","risk-publication-fixture");
                var kind=TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.RiskManagement;
                var draft=await db.GetSelectionPipelinePolicyAsync(kind,policy.ParameterSetId,1);draft!.Status.Should().Be(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogLifecycleStatus.Draft);
                await db.PublishAsync(TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyParameterSetKind.RiskManagement,policy.ParameterSetId,1,DateTime.UtcNow.AddSeconds(-1));
                var published=await db.GetSelectionPipelinePolicyAsync(kind,policy.ParameterSetId,1);
                published!.Status.Should().Be(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogLifecycleStatus.Published);
                published.PayloadSha256.Should().Be(policy.Hash());RiskParameterSet.Read(published.PayloadJson).TargetHorizon.Should().Be(horizon);
                await FluentActions.Awaiting(()=>db.InsertRiskManagementDraftAsync(policy with { MaximumUnits=11 },"Conflicting identity","fixture")).Should().ThrowAsync<Exception>();
                (await db.GetSelectionPipelinePolicyAsync(kind,policy.ParameterSetId,1))!.PayloadSha256.Should().Be(policy.Hash());
            }
        }
        finally { await supervisor.ShutdownAsync(); }
    }
    [Theory,Trait("Category","PortfolioFinancialRuntime"),Trait("Gate","PF-FIN-05")]
    [InlineData(TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily)]
    [InlineData(TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly)]
    [InlineData(TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly)]
    public async Task Real_risk_actor_qualifies_all_twelve_variants_for_one_triggering_horizon(TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType horizon)
    {
        var broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")??throw new InvalidOperationException("An isolated test broker is required.");
        await using var host=Host(brokerUrl:broker);_=host.CreateClient();
        var supervisor=host.Services.GetRequiredService<IActorSupervisor>();var producer=host.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime,$"RiskMatrix{Guid.NewGuid():N}"));
        try
        {
            foreach(var variant in TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer.CompositionFixture.Variants)
            {
                // Upstream market/configuration envelopes are labelled fixtures; the Risk calculation, actor and persistence are real.
                new RiskEvaluator().Calculate(await RiskFixture.Command(variant,horizon:horizon));
                var request=await RiskFixture.Command(variant,DateTime.UtcNow,$"RMM{Guid.NewGuid():N}",horizon);
                var reply=await producer.RequestFunctionAsync<ExecuteRiskManagementPipelineCommand,RiskManagementExecutionId,
                    FunctionResult<RiskManagementFunctionCompletedEvent,RiskManagementFunctionFailedEvent>>(request.Subject,request,request.EntityId);
                reply.Success.Should().BeTrue($"{horizon}/{variant}: {reply.ErrorMessage}");
                reply.Value!.IsCompleted.Should().BeTrue($"{horizon}/{variant}: {reply.Value.Failed?.ErrorMessage}");
                var result=reply.Value.Completed!.Result;
                result.Outcome.Should().Be(RiskAssessmentOutcome.Approved,$"{horizon}/{variant}: {string.Join(',',result.Reasons)}");
                result.TargetHorizon.Should().Be(horizon);result.StrategyUnits.Should().Be(10);
                result.Legs.Should().HaveCount(request.CompositionResult.ReadCompositionResult().Candidate!.Legs.Length);
                var repository=host.Services.GetRequiredService<SimpleInjector.Container>().GetInstance<IEventSourceFunctionStateRepository<RiskManagementFunctionState,ExecuteRiskManagementPipelineCommand>>();
                var restored=await repository.LoadStateAsync(request);RiskContracts.Hash(restored.CompletedEvent!.Result).Should().Be(RiskContracts.Hash(result));
            }
        }
        finally { await supervisor.ShutdownAsync();await producer.StopAsync(); }
    }
    [Fact,Trait("Category","PortfolioFinancialRuntime"),Trait("Gate","PF-FIN-05")]
    public async Task Audited_but_uncommitted_risk_preparation_is_resumed_by_the_actual_workflow_actor()
    {
        string broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")
            ?? throw new InvalidOperationException("An isolated IFM_FINANCIAL_TEST_NATS_URL is required.");
        await using var host=Host(brokerUrl:broker);_=host.CreateClient();
        var supervisor=host.Services.GetRequiredService<IActorSupervisor>();var producer=host.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime,"FinancialPreparationVerification"));
        try
        {
            var input=await RiskFixture.Command(atUtc:DateTime.UtcNow,contractId:$"RMQ{Guid.NewGuid():N}");var composition=input.CompositionResult.ReadCompositionResult();
            var command=new PrepareRiskManagementCommand { CommandId=Guid.NewGuid(),EntityId=input.WorkflowEntityId,
                Subject=new(ActorType.Command,PrepareRiskManagementCommand.Actor,PrepareRiskManagementCommand.Verb,input.WorkflowEntityId.Format()),
                WorkflowId=input.WorkflowId,InputWorkflowRevision=5 };
            var view=new IntrinsicTimeStrategyWorkflowView { EntityId=input.WorkflowEntityId,WorkflowId=input.WorkflowId,WorkflowRevision=5,
                Status=WorkflowStrategyMachineStatus.Started,CurrentStage=StrategyWorkflowStage.RiskManagement,ExpiresAtUtc=input.ExpiresAtUtc,
                CorrelationId=input.CorrelationId,OrderComposition=new() { Result=input.CompositionResult,SourceEventId=composition.ResultId },
                RiskManagement=new() { ProcessingStatus=StrategyActorProcessingStatus.Processing,InputWorkflowRevision=5 },
                SelectionBinding=new() { CatalogDefinitions=[new() { Key=composition.Candidate!.DeploymentKey,PipelineParameters=[] }] } };
            var seed=new WorkflowStrategyStateUpdatedEvent { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=view.EntityId,State=view,
                WorkflowId=view.WorkflowId,WorkflowRevision=5,AggregateId=view.EntityId.Format(),ReceivedOn=DateTime.UtcNow,
                Subject=new(ActorType.Event,WorkflowStrategyStateUpdatedEvent.Actor,WorkflowStrategyStateUpdatedEvent.Verb,view.EntityId.Format()) };
            var db=host.Services.GetRequiredService<IEventSourceActorDbContext>();
            await db.SaveEventsAsync(command.StreamId,seed.CommandId,new DomainEventCollection([seed]),0,CancellationToken.None);
            (await ((ICommandAuditLogger)db).TryReserveAsync(command)).Accepted.Should().BeTrue();
            var reply=await producer.RequestAsync<PrepareRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(command.Subject,command,command.EntityId);
            reply.Success.Should().BeTrue(reply.ErrorMessage);
            var container=host.Services.GetRequiredService<SimpleInjector.Container>();
            var repository=container.GetInstance<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
            var actual=(await repository.LoadStateAsync(command)).CurrentView!;
            actual.WorkflowRevision.Should().Be(6);
            actual.RiskManagement.Failure!.ErrorType.Should().Be("RiskPreparationFailed");
            actual.RiskManagement.Failure.ErrorMessage.Should().Be("RM.CONFIG.MISSING");
            // Duplicate confirmation must not turn a durable failed preparation into a different invocation.
            var replay=await producer.RequestAsync<PrepareRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId,GuidResult>(command.Subject,command,command.EntityId);
            replay.Success.Should().BeTrue(replay.ErrorMessage);
            (await repository.LoadStateAsync(command)).CurrentView!.WorkflowRevision.Should().Be(6);
        }
        finally { await supervisor.ShutdownAsync();await producer.StopAsync(); }
    }
    [Fact,Trait("Category","PortfolioFinancialRuntime"),Trait("Gate","PF-FIN-05")]
    public async Task Risk_real_actor_pool_NATS_Postgres_completion_reconstruction_and_conflict()
    {
        string broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")
            ?? throw new InvalidOperationException("An isolated IFM_FINANCIAL_TEST_NATS_URL is required.");
        await using var host=Host(brokerUrl:broker); _=host.CreateClient();
        var supervisor=host.Services.GetRequiredService<IActorSupervisor>();
        var producer=host.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime,"FinancialRiskVerification"));
        try
        {
            // Warm the calculation/JIT before creating the one-second live input; no production deadline override.
            new RiskEvaluator().Calculate(await RiskFixture.Command());
            var request=await RiskFixture.Command(atUtc:DateTime.UtcNow,contractId:$"RMQ{Guid.NewGuid():N}");
            var reply=await producer.RequestFunctionAsync<ExecuteRiskManagementPipelineCommand,RiskManagementExecutionId,
                FunctionResult<RiskManagementFunctionCompletedEvent,RiskManagementFunctionFailedEvent>>(request.Subject,request,request.EntityId);
            reply.Success.Should().BeTrue(reply.ErrorMessage);
            reply.Value!.IsCompleted.Should().BeTrue(reply.Value.Failed?.ErrorMessage);
            var completed=reply.Value.Completed!;
            completed.CapacityAssessment.Eligible.Should().BeTrue(); completed.EventId.Should().BePositive();
            var container=host.Services.GetRequiredService<SimpleInjector.Container>();
            container.GetInstance<IRiskManagementFunctionContext>().Should().BeSameAs(container.GetInstance<IFunctionActorContext<RiskManagementFunctionActor>>());
            var repository=container.GetInstance<IEventSourceFunctionStateRepository<RiskManagementFunctionState,ExecuteRiskManagementPipelineCommand>>();
            var reconstructed=await repository.LoadStateAsync(request);
            reconstructed.Matches(request).Should().BeTrue();
            RiskContracts.Hash(reconstructed.CompletedEvent!.Result).Should().Be(RiskContracts.Hash(completed.Result));
            var replay=await producer.RequestFunctionAsync<ExecuteRiskManagementPipelineCommand,RiskManagementExecutionId,
                FunctionResult<RiskManagementFunctionCompletedEvent,RiskManagementFunctionFailedEvent>>(request.Subject,request,request.EntityId);
            replay.Value!.Completed!.Id.Should().Be(completed.Id);
            var changed=request with { IncrementalLossReserve=1 }; changed=changed with { InputSha256=changed.Fingerprint() };
            var conflict=await producer.RequestFunctionAsync<ExecuteRiskManagementPipelineCommand,RiskManagementExecutionId,
                FunctionResult<RiskManagementFunctionCompletedEvent,RiskManagementFunctionFailedEvent>>(changed.Subject,changed,changed.EntityId);
            conflict.Value!.Failed!.ReasonCode.Should().Be("RM.INPUT.CONFLICTING_DUPLICATE");
        }
        finally { await supervisor.ShutdownAsync(); await producer.StopAsync(); }
    }
}
