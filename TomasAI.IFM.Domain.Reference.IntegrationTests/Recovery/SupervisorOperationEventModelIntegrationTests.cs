using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests.Recovery;

[Collection(ReferenceIntegrationInfrastructureCollection.Name)]
public sealed class SupervisorOperationEventModelIntegrationTests(
    ReferenceIntegrationInfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Unsupported_stop_commits_an_outcome_and_projects_its_typed_fail_event()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var commandId = Guid.NewGuid();
        var entityId = ActorEntityId.Default;
        var command = new StopSupervisorActorCommand
        {
            CommandId = commandId,
            Subject = new(ActorType.Command, StopSupervisorActorCommand.Actor,
                StopSupervisorActorCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Target = new(ActorType.Command, "ReferenceIntegrationTests", "qualifier"),
            ExpectedGeneration = 0,
            Requester = "IFM.UI.Development",
            Reason = "Isolated event-model qualification",
            TimeoutTicks = TimeSpan.FromSeconds(10).Ticks
        };

        var reply = await infrastructure.ActorProducer
            .RequestAsync<StopSupervisorActorCommand, ActorEntityId, GuidResult>(
                command.Subject, command, entityId, deadline.Token);

        Assert.False(reply.Success);
        while (!deadline.IsCancellationRequested &&
            !infrastructure.SupervisorOperations.RecentOperations.Any(
                operation => operation.OperationId == commandId))
            await Task.Delay(50, deadline.Token);
        var projected = Assert.Single(infrastructure.SupervisorOperations.RecentOperations,
            operation => operation.OperationId == commandId);
        Assert.Equal(SupervisorOperationOutcome.Rejected, projected.Outcome);
    }
}
