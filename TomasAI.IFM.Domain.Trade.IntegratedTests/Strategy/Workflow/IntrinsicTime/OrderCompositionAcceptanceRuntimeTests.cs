using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;
public sealed partial class TradeSelectionRuntimeTests
{
    [Theory, InlineData(false), InlineData(true), Trait("Gate", "OC-06"), Trait("Gate", "OC-07")]
    public async Task OrderComposer_workflow_acceptance_is_durable_once_and_queries_do_not_depend_on_Scylla_workflow_projection(bool empty)
    {
        // Withhold the conventional workflow projection/notification, preserving real PostgreSQL authority.
        var projector = Substitute.For<IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>>();
        projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns(ValueTask.CompletedTask);
        await using var factory = Host(services =>
        {
            var container = (SimpleInjector.Container)services.Single(x => x.ServiceType == typeof(SimpleInjector.Container)).ImplementationInstance!;
            container.RegisterInstance(projector);
        });
        _ = factory.CreateClient(); var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        var producer = factory.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime, "ComposerAcceptanceVerification"));
        try
        {
            await factory.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            var c = await CompositionFixture.Command(atUtc: DateTime.UtcNow, integrationTiming: true, contractId: "ES.TEST." + Guid.NewGuid().ToString("N"));
            if (empty)
            {
                var snapshot = c.MarketSnapshot with { Instruments = [], Digest = "" };
                c = CompositionFixture.Seal(c with { MarketSnapshot = snapshot with { Digest = CompositionSemanticHash.Compute(snapshot) } });
            }
            var reply = await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand, OrderCompositionExecutionId,
                FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>(c.Subject, c, c.EntityId);
            reply.Value!.IsCompleted.Should().BeTrue(reply.ErrorMessage);
            var completed = CompositionFixture.Completion(c, reply.Value.Completed!.Result.ReadCompositionResult()) with
                { Subject = Subject(CompleteOrderCompositionCommand.Verb, c.WorkflowEntityId) };
            var seed = new WorkflowStrategyStateUpdatedEvent { Id = Guid.NewGuid(), EntityId = c.WorkflowEntityId, WorkflowId = c.WorkflowId,
                WorkflowRevision = c.InputWorkflowRevision, State = c.WorkflowView with { CompositionExecution = c },
                Subject = new(ActorType.Event, CompleteOrderCompositionCommand.Actor, WorkflowStrategyStateUpdatedEvent.Verb, c.WorkflowEntityId.Format()) };
            await factory.Services.GetRequiredService<IEventSourceActorDbContext>().SaveEventsAsync(completed.StreamId, Guid.NewGuid(), new DomainEventCollection([seed]), 0, default);
            var accepted = await producer.RequestAsync<CompleteOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId, GuidResult>(completed.Subject, completed, completed.EntityId);
            accepted.Success.Should().BeTrue(accepted.ErrorMessage);
            var repository = factory.Services.GetRequiredService<SimpleInjector.Container>().GetInstance<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
            var restored = await repository.LoadStateAsync(completed);
            restored.CurrentView!.WorkflowRevision.Should().Be(c.InputWorkflowRevision + 1);
            restored.CurrentView.CurrentStage.Should().Be(empty ? StrategyWorkflowStage.OrderComposition : StrategyWorkflowStage.RiskManagement);
            restored.CurrentView.Outcome.Should().Be(empty ? StrategyWorkflowOutcome.NoTrade : StrategyWorkflowOutcome.None);
            await producer.RequestAsync<CompleteOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId, GuidResult>(completed.Subject, completed, completed.EntityId);
            (await repository.LoadStateAsync(completed)).PersistedStreamVersion.Should().Be(restored.PersistedStreamVersion);
            var query = await new OrderCompositionQueryApi(producer).GetInvocationAsync(c.WorkflowId, c.CommandId);
            query.Success.Should().BeTrue(query.ErrorMessage); query.Value!.WorkflowAccepted.Should().BeTrue();
            query.Value.AcceptanceUnknown.Should().BeFalse(); query.Value.SuspectedOrphan.Should().BeFalse();
        }
        finally { await supervisor.ShutdownAsync(); await producer.StopAsync(); }
    }
}
