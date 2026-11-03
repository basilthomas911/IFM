using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Validation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskConventionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 20, 0, 0, TimeSpan.Zero);
    private static readonly ScheduledTaskId Id = new(Guid.NewGuid());

    [Fact]
    public void Independent_payload_errors_accumulate_and_nulls_never_throw()
    {
        var command = new CreateScheduledTaskCommand { ExpectedRevision = -1, Schedule = null!, Operator = null!, Reason = null! };
        var errors = new List<ValidationError>().ValidateScheduledTaskCommand(command);
        Assert.Equal(5, errors.Count);
        var invalid = new ScheduledTaskSchedule { Name = "", TaskKey = "", MaximumRuntimeSeconds = 0, Timing = (ScheduledTaskTiming)99 };
        Assert.True(new List<ValidationError>().ValidateScheduledTaskCommand(command with { Schedule = invalid }).Count >= 6);
    }

    [Fact]
    public void A_valid_payload_cannot_target_a_different_aggregate_stream()
    {
        var command = new CreateScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id,
            Subject = new(ActorType.Command, ScheduledTaskCommandActor.Actor, CreateScheduledTaskCommand.Verb, new ScheduledTaskId(Guid.NewGuid()).Format()),
            Schedule = new() { Name = "Close", TaskKey = "close", Expression = "0 0 17 ? * MON-FRI" } };
        var errors = new List<ValidationError>().ValidateScheduledTaskCommand(command);
        Assert.Contains("Subject", Assert.Single(errors).ErrorMessage);
        Assert.Empty(new List<ValidationError>().ValidateScheduledTaskCommand(command with
        { Subject = new(ActorType.Command, ScheduledTaskCommandActor.Actor, CreateScheduledTaskCommand.Verb, Id.Format()) }));
    }

    [Fact]
    public async Task Handler_uses_clock_without_catalog_IO_and_replays_its_exact_committed_payload()
    {
        var repository = Substitute.For<IEventSourceActorStateRepository<ScheduledTaskCommandState>>();
        var projector = Substitute.For<IEventProjector<ScheduledTaskCommandActor>>();
        var clock = Substitute.For<TimeProvider>(); clock.GetUtcNow().Returns(Now);
        var context = new ScheduledTaskCommandContext(Substitute.For<IActorSupervisor>(), repository, projector,
            NullLogger<ScheduledTaskCommandActor>.Instance, clock);
        var command = new CreateScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id,
            Subject = new(ActorType.Command, ScheduledTaskCommandActor.Actor, CreateScheduledTaskCommand.Verb, Id.Format()),
            Schedule = new() { Name = "Close", TaskKey = "close", Expression = "0 0 17 ? * MON-FRI" } };
        var live = new ScheduledTaskCommandState();
        Assert.IsType<ServiceOk<GuidResult>>(command.Execute(context, live));
        var source = Assert.IsType<ScheduledTaskCreatedEvent>(Assert.Single(live.Events));
        Assert.Equal(command.CommandId, source.CommandId);
        Assert.Equal(Now, source.ScheduledTaskDefinition!.UpdatedAtUtc);
        var replayed = new ScheduledTaskCommandState();
        replayed.ReplayEvents(new DomainEventCollection(live.Events));
        Assert.Equal(live.ScheduledTaskDefinition, replayed.ScheduledTaskDefinition);
        Assert.Empty(replayed.Events); Assert.False(replayed.Updated);
        Assert.Single(live.Events);
        await repository.DidNotReceiveWithAnyArgs().SaveStateAsync(default!, default!, default!);
        await projector.DidNotReceiveWithAnyArgs().DomainEventsProjectionAsync(default!);
    }

    [Fact]
    public async Task Missing_catalog_evidence_rejects_without_applying_or_publishing_an_event()
    {
        var projector = Substitute.For<IEventProjector<ScheduledTaskCommandActor>>();
        var context = new ScheduledTaskCommandContext(Substitute.For<IActorSupervisor>(),
            Substitute.For<IEventSourceActorStateRepository<ScheduledTaskCommandState>>(), projector,
            NullLogger<ScheduledTaskCommandActor>.Instance);
        var state = new ScheduledTaskCommandState();
        Assert.True(state.Apply(new ScheduledTaskCreatedEvent { EntityId = Id, ScheduledTaskDefinition = new()
            { Id = Id, Revision = 1, Schedule = new() { Name = "Close", TaskKey = "close", Expression = "0 0 17 ? * MON-FRI" } } }));
        state.AcceptChanges(); var accepted = state.ScheduledTaskDefinition;
        var command = new EnableScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id, ExpectedRevision = 1 };
        Assert.IsType<ServiceFailed<GuidResult>>(command.Execute(context, state));
        Assert.Same(accepted, state.ScheduledTaskDefinition); Assert.Empty(state.Events); Assert.False(state.Updated);
        await projector.DidNotReceiveWithAnyArgs().DomainEventsProjectionAsync(default!);
    }

    [Theory]
    [InlineData(ScheduledTaskKeys.FuturesMarketOpen)]
    [InlineData(ScheduledTaskKeys.FuturesMarketClose)]
    public void Development_lifecycle_admission_does_not_depend_on_catalog_availability(string taskKey)
    {
        var context = Context(Now);
        var state = new ScheduledTaskCommandState();
        Assert.True(state.Apply(new ScheduledTaskCreatedEvent { EntityId = Id, ScheduledTaskDefinition = new()
            { Id = Id, Revision = 1, Schedule = new() { TaskKey = taskKey, Environment = "Development", HostId = "development" } } }));
        state.AcceptChanges();
        var command = new AdmitScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = Id, RunId = Guid.NewGuid(),
            IntendedFireTimeUtc = Now, Environment = "Development", HostId = "development",
            Subject = new(ActorType.Command, AdmitScheduledTaskRunCommand.Actor, AdmitScheduledTaskRunCommand.Verb, Id.Format()) };
        Assert.IsType<ServiceOk<GuidResult>>(command.Execute(context, state));
        Assert.Equal(command.RunId, state.ScheduledTaskDefinition!.ActiveRunId);
        Assert.IsType<ScheduledTaskRunAdmittedEvent>(Assert.Single(state.Events));
    }

    [Fact]
    public void Rejected_transition_keeps_business_state_and_pending_events_unchanged()
    {
        var state = new ScheduledTaskCommandState();
        var command = new CreateScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id,
            Subject = new(ActorType.Command, ScheduledTaskCommandActor.Actor, CreateScheduledTaskCommand.Verb, Id.Format()),
            Schedule = new() { Name = "Close", TaskKey = "close", Expression = "0 0 17 ? * MON-FRI" } };
        Assert.IsType<ServiceOk<GuidResult>>(command.Execute(Context(Now), state));
        state.AcceptChanges(); var accepted = state.ScheduledTaskDefinition;
        Assert.IsType<ServiceFailed<GuidResult>>(command.Execute(Context(Now), state));
        Assert.Same(accepted, state.ScheduledTaskDefinition); Assert.Empty(state.Events); Assert.False(state.Updated);
    }

    [Fact]
    public void Every_state_rejects_cross_identity_and_nonconsecutive_events_before_mutation()
    {
        CheckDefinition(); CheckRun(); CheckCatalog();
    }
    internal static ScheduledTaskCommandContext Context(DateTimeOffset now)
    {
        var clock = Substitute.For<TimeProvider>(); clock.GetUtcNow().Returns(now);
        return new(Substitute.For<IActorSupervisor>(), Substitute.For<IEventSourceActorStateRepository<ScheduledTaskCommandState>>(),
            Substitute.For<IEventProjector<ScheduledTaskCommandActor>>(),
            NullLogger<ScheduledTaskCommandActor>.Instance, clock);
    }

    private static void CheckDefinition()
    {
        var state = new ScheduledTaskCommandState();
        var source = new ScheduledTaskCreatedEvent { EntityId = Id, ScheduledTaskDefinition = new() { Id = Id, Revision = 1 } };
        Assert.True(state.Apply(source)); state.AcceptChanges();
        var other = new ScheduledTaskId(Guid.NewGuid());
        Assert.False(state.Apply(source with { EntityId = other, ScheduledTaskDefinition = source.ScheduledTaskDefinition! with { Id = other, Revision = 2 } }));
        Assert.False(state.Apply(source with { ScheduledTaskDefinition = source.ScheduledTaskDefinition! with { Revision = 3 } }));
        Assert.Equal(Id, state.ScheduledTaskDefinition!.Id); Assert.Equal(1, state.ScheduledTaskDefinition.Revision); Assert.Empty(state.Events);
        Assert.True(state.Apply(source with { ScheduledTaskDefinition = source.ScheduledTaskDefinition! with { Revision = 2 } }));
        var replayed = new ScheduledTaskCommandState();
        replayed.ReplayEvents(new DomainEventCollection { source, source with { ScheduledTaskDefinition = source.ScheduledTaskDefinition! with { Revision = 2 } } });
        Assert.Equal(state.ScheduledTaskDefinition, replayed.ScheduledTaskDefinition); Assert.Empty(replayed.Events); Assert.False(replayed.Updated);
        Assert.False(state.Apply(source));

    }
    private static void CheckRun()
    {
        var state = new ScheduledTaskRunCommandState();
        var source = new ScheduledTaskRunRequestedEvent { EntityId = Id, ScheduledTaskRun = new() { Id = Id, Revision = 1 } };
        Assert.True(state.Apply(source)); state.AcceptChanges();
        var other = new ScheduledTaskId(Guid.NewGuid());
        Assert.False(state.Apply(source with { EntityId = other, ScheduledTaskRun = source.ScheduledTaskRun! with { Id = other, Revision = 2 } }));
        Assert.False(state.Apply(source with { ScheduledTaskRun = source.ScheduledTaskRun! with { Revision = 3 } }));
        Assert.Equal(Id, state.ScheduledTaskRun!.Id); Assert.Equal(1, state.ScheduledTaskRun.Revision); Assert.Empty(state.Events);
        Assert.True(state.Apply(source with { ScheduledTaskRun = source.ScheduledTaskRun! with { Revision = 2 } }));
        var replayed = new ScheduledTaskRunCommandState();
        replayed.ReplayEvents(new DomainEventCollection { source, source with { ScheduledTaskRun = source.ScheduledTaskRun! with { Revision = 2 } } });
        Assert.Equal(state.ScheduledTaskRun, replayed.ScheduledTaskRun); Assert.Empty(replayed.Events); Assert.False(replayed.Updated);
        Assert.False(state.Apply(source));

    }
    private static void CheckCatalog()
    {
        var state = new ScheduledTaskCatalogCommandState();
        var source = new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = Id, ScheduledTaskCatalog = new() { Id = Id, Revision = 1 } };
        Assert.True(state.Apply(source)); state.AcceptChanges();
        var other = new ScheduledTaskId(Guid.NewGuid());
        Assert.False(state.Apply(source with { EntityId = other, ScheduledTaskCatalog = source.ScheduledTaskCatalog! with { Id = other, Revision = 2 } }));
        Assert.False(state.Apply(source with { ScheduledTaskCatalog = source.ScheduledTaskCatalog! with { Revision = 3 } }));
        Assert.Equal(Id, state.ScheduledTaskCatalog!.Id); Assert.Equal(1, state.ScheduledTaskCatalog.Revision); Assert.Empty(state.Events);
        Assert.True(state.Apply(source with { ScheduledTaskCatalog = source.ScheduledTaskCatalog! with { Revision = 2 } }));
        var replayed = new ScheduledTaskCatalogCommandState();
        replayed.ReplayEvents(new DomainEventCollection { source, source with { ScheduledTaskCatalog = source.ScheduledTaskCatalog! with { Revision = 2 } } });
        Assert.Equal(state.ScheduledTaskCatalog, replayed.ScheduledTaskCatalog); Assert.Empty(replayed.Events); Assert.False(replayed.Updated);
        Assert.False(state.Apply(source));

    }
}
