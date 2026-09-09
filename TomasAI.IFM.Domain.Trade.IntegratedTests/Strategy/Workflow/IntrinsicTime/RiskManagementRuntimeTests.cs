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
