using TomasAI.IFM.Application.EventModelActor;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests.ScheduledTask;

/// <summary>Isolated command-handler scenarios with fixed time and real command/event wire round trips.</summary>
internal sealed class ScheduledTaskScenario
{
    internal static readonly DateTimeOffset Now = new(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);
    internal static readonly DateOnly ValueDate = new(2026, 11, 3);
    internal ScheduledTaskId Id { get; } = new(Guid.NewGuid());
    internal ScheduledTaskId CatalogId => ScheduledTaskIdentities.Catalog("Production", "bdd-host");
    internal ScheduledTaskId RunId => ScheduledTaskIdentities.Occurrence(Id, Fire);
    internal Guid AdmissionId { get; } = Guid.NewGuid();
    internal Guid OperationId { get; } = Guid.NewGuid();
    internal DateTimeOffset Fire => Now.AddSeconds(-10);
    internal string TaskKey { get; }
    internal ScheduledTaskCommandState DefinitionState { get; private set; } = new();
    internal ScheduledTaskRunCommandState RunState { get; private set; } = new();
    internal ScheduledTaskCatalogCommandState CatalogState { get; private set; } = new();
    internal ScheduledTaskDefinition Definition => DefinitionState.ScheduledTaskDefinition!;
    internal ScheduledTaskRun Run => RunState.ScheduledTaskRun!;
    internal ScheduledTaskCatalog Catalog => CatalogState.ScheduledTaskCatalog!;
    private readonly ScenarioClock clock = new();
    private readonly ScheduledTaskCommandContext _definitionContext;
    private readonly ScheduledTaskRunCommandContext _runContext;
    private readonly ScheduledTaskCatalogCommandContext _catalogContext;

    internal ScheduledTaskScenario(string taskKey = "qualification")
    {
        TaskKey = taskKey;
        var supervisor = Substitute.For<IActorSupervisor>();
        _definitionContext = new(supervisor, Substitute.For<IEventSourceActorStateRepository<ScheduledTaskCommandState>>(),
            Substitute.For<IEventProjector<ScheduledTaskCommandActor>>(), NullLogger<ScheduledTaskCommandActor>.Instance, clock);
        _runContext = new(supervisor, Substitute.For<IEventSourceActorStateRepository<ScheduledTaskRunCommandState>>(),
            Substitute.For<IEventProjector<ScheduledTaskRunCommandActor>>(), NullLogger<ScheduledTaskRunCommandActor>.Instance, clock);
        _catalogContext = new(supervisor, Substitute.For<IEventSourceActorStateRepository<ScheduledTaskCatalogCommandState>>(),
            Substitute.For<IEventProjector<ScheduledTaskCatalogCommandActor>>(), NullLogger<ScheduledTaskCatalogCommandActor>.Instance, clock);
    }

    internal ScheduledTaskSchedule Schedule => new()
    {
        TaskKey = TaskKey, Name = "BDD qualification", Environment = "Production", HostId = "bdd-host",
        Timing = ScheduledTaskTiming.Cron, Expression = "0 * * ? * *", TimeZoneId = "UTC",
        MaximumRuntimeSeconds = 120, DispatchToleranceSeconds = 60
    };
    internal ScheduledTaskProject Project => new()
    {
        TaskKey = TaskKey, ProjectName = "Qualification", Available = true, ManifestVersion = "v1",
        ArtifactDigest = "sha256:reviewed", Platform = "Windows", MaximumRuntimeSeconds = 300
    };
    internal ScheduledTaskHostCapability Capability => new()
    {
        Environment = "Production", HostId = "bdd-host", Platform = "Windows", Ready = true,
        Generation = 2, ObservedAtUtc = Now.AddSeconds(-1)
    };

    internal ScheduledTaskScenario GivenDefinition() { Assert.True(Send(Create()).Success); return this; }
    internal ScheduledTaskScenario GivenCatalog() { Assert.True(Send(Register()).Success); return this; }
    internal ScheduledTaskScenario GivenReadyCatalog() { GivenCatalog(); Assert.True(Send(Observe()).Success); return this; }
    internal ScheduledTaskScenario GivenEnabled() { GivenDefinition(); GivenReadyCatalog(); Assert.True(Send(Enable()).Success); return this; }
    internal ScheduledTaskScenario GivenInstalled() { GivenEnabled(); Assert.True(Send(Installation()).Success); return this; }
    internal ScheduledTaskScenario GivenAdmittedOccurrence() { GivenInstalled(); Assert.True(Send(Admit()).Success); return this; }
    internal ScheduledTaskScenario GivenRequestedRun() { Assert.True(Send(RequestRun()).Success); return this; }
    internal ScheduledTaskScenario GivenAdmittedRun() { GivenRequestedRun(); Assert.True(Send(RunAdmission()).Success); return this; }
    internal ScheduledTaskScenario GivenRunningRun() { GivenAdmittedRun(); Assert.True(Send(Start()).Success); return this; }

    internal void RestartFromRecordedEvents()
    {
        DefinitionState = Restore(DefinitionState);
        RunState = Restore(RunState);
        CatalogState = Restore(CatalogState);

        static TState Restore<TState>(TState previous) where TState : BaseEventSourceActorState<TState>, new()
        {
            var restored = new TState { Id = previous.Id };
            restored.ReplayEvents(previous.Events.Select(RoundTrip));
            return restored;
        }
    }

    // Every scenario enters through the public concrete handler after deserializing its message.
    internal ServiceResult<GuidResult> Send(ICommand<ScheduledTaskId> command)
    {
        var origin = MessageOrigins.ForPublication(command, clock, "scheduler-bdd");
        var received = (ICommand<ScheduledTaskId>)MessagePackSerializer.Deserialize(command.GetType(),
            MessagePackSerializer.Serialize(command.GetType(), command))!;
        MessageOrigins.Restore(received, MessageOriginJson.Deserialize(MessageOriginJson.Serialize(origin)));
        var result = received switch
        {
            CreateScheduledTaskCommand c => c.Execute(_definitionContext, DefinitionState),
            ChangeScheduledTaskScheduleCommand c => c.Execute(_definitionContext, DefinitionState),
            EnableScheduledTaskCommand c => c.Execute(_definitionContext, DefinitionState),
            DisableScheduledTaskCommand c => c.Execute(_definitionContext, DefinitionState),
            RemoveScheduledTaskCommand c => c.Execute(_definitionContext, DefinitionState),
            RecordScheduledTaskInstallationCommand c => c.Execute(_definitionContext, DefinitionState),
            RecordScheduledTaskInstallationFailureCommand c => c.Execute(_definitionContext, DefinitionState),
            AdmitScheduledTaskRunCommand c => c.Execute(_definitionContext, DefinitionState),
            RecordScheduledTaskRunCompletionCommand c => c.Execute(_definitionContext, DefinitionState),
            RequestScheduledTaskRunCommand c => c.Execute(_runContext, RunState),
            RecordScheduledTaskRunAdmissionCommand c => c.Execute(_runContext, RunState),
            RecordScheduledTaskRunStartedCommand c => c.Execute(_runContext, RunState),
            RecordScheduledTaskRunStageCommand c => c.Execute(_runContext, RunState),
            CompleteScheduledTaskRunCommand c => c.Execute(_runContext, RunState),
            FailScheduledTaskRunCommand c => c.Execute(_runContext, RunState),
            RecordScheduledTaskRunUncertainCommand c => c.Execute(_runContext, RunState),
            RegisterScheduledTaskProjectCommand c => c.Execute(_catalogContext, CatalogState),
            RecordScheduledTaskHostCapabilityCommand c => c.Execute(_catalogContext, CatalogState),
            _ => throw new ArgumentException($"Scenario has no handler for {command.CommandName}.", nameof(command))
        };
        foreach (var source in DefinitionState.Events.Concat(RunState.Events).Concat(CatalogState.Events))
            MessageOrigins.ForPublication(source, clock, "scheduler-bdd");
        return result;
    }

    private static IEvent RoundTrip(IEvent source)
    {
        var restored = (IEvent)MessagePackSerializer.Deserialize(source.GetType(),
            MessagePackSerializer.Serialize(source.GetType(), source))!;
        return MessageOrigins.Restore(restored,
            MessageOriginJson.Deserialize(MessageOriginJson.Serialize(MessageOrigins.Get(source))));
    }

    internal CreateScheduledTaskCommand Create() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, CreateScheduledTaskCommand.Actor, CreateScheduledTaskCommand.Verb, Id.Format()),
        Schedule = Schedule, ExpectedRevision = 0
    };

    internal ChangeScheduledTaskScheduleCommand Change() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, ChangeScheduledTaskScheduleCommand.Actor, ChangeScheduledTaskScheduleCommand.Verb, Id.Format()),
        Schedule = Schedule with { Expression = "0 5 * ? * *" }, ExpectedRevision = Definition.Revision
    };

    internal EnableScheduledTaskCommand Enable() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, EnableScheduledTaskCommand.Actor, EnableScheduledTaskCommand.Verb, Id.Format()),
        ExpectedRevision = Definition.Revision, CatalogEvidence = Catalog
    };

    internal DisableScheduledTaskCommand Disable() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, DisableScheduledTaskCommand.Actor, DisableScheduledTaskCommand.Verb, Id.Format()),
        ExpectedRevision = Definition.Revision
    };

    internal RemoveScheduledTaskCommand Remove() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RemoveScheduledTaskCommand.Actor, RemoveScheduledTaskCommand.Verb, Id.Format()),
        ExpectedRevision = Definition.Revision
    };

    internal RecordScheduledTaskInstallationCommand Installation() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskInstallationCommand.Actor, RecordScheduledTaskInstallationCommand.Verb, Id.Format()),
        DesiredRevision = Definition.DesiredRevision, AppliedEnabled = Definition.Enabled && !Definition.Removed, Fingerprint = "installed-sha256"
    };

    internal RecordScheduledTaskInstallationFailureCommand InstallationFailure() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskInstallationFailureCommand.Actor, RecordScheduledTaskInstallationFailureCommand.Verb, Id.Format()),
        DesiredRevision = Definition.DesiredRevision, Detail = "runtime installation failed"
    };

    internal AdmitScheduledTaskRunCommand Admit() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, AdmitScheduledTaskRunCommand.Actor, AdmitScheduledTaskRunCommand.Verb, Id.Format()),
        RunId = AdmissionId, DefinitionRevision = Definition.DesiredRevision, IntendedFireTimeUtc = Now, HostId = "bdd-host", Environment = "Production", CatalogEvidence = Catalog
    };

    internal RecordScheduledTaskRunCompletionCommand Release() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskRunCompletionCommand.Actor, RecordScheduledTaskRunCompletionCommand.Verb, Id.Format()),
        RunId = AdmissionId
    };

    internal RequestScheduledTaskRunCommand RequestRun() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = RunId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RequestScheduledTaskRunCommand.Actor, RequestScheduledTaskRunCommand.Verb, RunId.Format()),
        ScheduleId = Id, DefinitionRevision = 1, IntendedFireTimeUtc = Fire, TaskKey = TaskKey, HostId = "bdd-host", Environment = "Production"
    };

    internal RecordScheduledTaskRunAdmissionCommand RunAdmission() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = RunId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskRunAdmissionCommand.Actor, RecordScheduledTaskRunAdmissionCommand.Verb, RunId.Format()),
        Accepted = true, Detail = "schedule admitted occurrence"
    };

    internal RecordScheduledTaskRunStartedCommand Start() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = RunId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskRunStartedCommand.Actor, RecordScheduledTaskRunStartedCommand.Verb, RunId.Format()),
        ProcessId = 1234, StartedAtUtc = Fire.AddSeconds(1)
    };

    internal RecordScheduledTaskRunStageCommand Stage() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = RunId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskRunStageCommand.Actor, RecordScheduledTaskRunStageCommand.Verb, RunId.Format()),
        Stage = "FeedsStopped", ValueDate = ValueDate, Detail = "feed shutdown confirmed"
    };

    internal CompleteScheduledTaskRunCommand Complete() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = RunId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, CompleteScheduledTaskRunCommand.Actor, CompleteScheduledTaskRunCommand.Verb, RunId.Format()),
        FinishedAtUtc = Now, ExitCode = 0, Stage = "BusinessCompleted", ValueDate = ValueDate, Detail = "all work confirmed"
    };

    internal FailScheduledTaskRunCommand Fail() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = RunId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, FailScheduledTaskRunCommand.Actor, FailScheduledTaskRunCommand.Verb, RunId.Format()),
        FinishedAtUtc = Now, ExitCode = 1, Stage = "ProcessFailed", Detail = "task reported failure"
    };

    internal RecordScheduledTaskRunUncertainCommand Uncertain() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = RunId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskRunUncertainCommand.Actor, RecordScheduledTaskRunUncertainCommand.Verb, RunId.Format()),
        FinishedAtUtc = Now, Detail = "host outcome could not be confirmed"
    };

    internal RegisterScheduledTaskProjectCommand Register() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = CatalogId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RegisterScheduledTaskProjectCommand.Actor, RegisterScheduledTaskProjectCommand.Verb, CatalogId.Format()),
        HostId = "bdd-host", Environment = "Production", Project = Project
    };

    internal RecordScheduledTaskHostCapabilityCommand Observe() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = CatalogId, OperationCommandId = OperationId,
        Operator = "bdd-operator", Reason = "isolated behavior scenario",
        Subject = new(ActorType.Command, RecordScheduledTaskHostCapabilityCommand.Actor, RecordScheduledTaskHostCapabilityCommand.Verb, CatalogId.Format()),
        HostCapability = Capability
    };

    internal void ThenDefinitionAccepted<TEvent>(ICommand<ScheduledTaskId> command, Action<ScheduledTaskDefinition> then)
        where TEvent : class, IEvent
        => Accepted<ScheduledTaskCommandState, ScheduledTaskDefinition, TEvent>(command, DefinitionState, s => s.ScheduledTaskDefinition, then);
    internal void ThenRunAccepted<TEvent>(ICommand<ScheduledTaskId> command, Action<ScheduledTaskRun> then)
        where TEvent : class, IEvent
        => Accepted<ScheduledTaskRunCommandState, ScheduledTaskRun, TEvent>(command, RunState, s => s.ScheduledTaskRun, then);
    internal void ThenCatalogAccepted<TEvent>(ICommand<ScheduledTaskId> command, Action<ScheduledTaskCatalog> then)
        where TEvent : class, IEvent
        => Accepted<ScheduledTaskCatalogCommandState, ScheduledTaskCatalog, TEvent>(command, CatalogState, s => s.ScheduledTaskCatalog, then);
    internal void ThenDefinitionRejected(ICommand<ScheduledTaskId> command, string code)
        => Rejected(command, DefinitionState, s => s.ScheduledTaskDefinition, code);
    internal void ThenRunRejected(ICommand<ScheduledTaskId> command, string code)
        => Rejected(command, RunState, s => s.ScheduledTaskRun, code);
    internal void ThenCatalogRejected(ICommand<ScheduledTaskId> command, string code)
        => Rejected(command, CatalogState, s => s.ScheduledTaskCatalog, code);

    private void Accepted<TState, TSnapshot, TEvent>(ICommand<ScheduledTaskId> command, TState state,
        Func<TState, TSnapshot?> read, Action<TSnapshot> then)
        where TState : BaseEventSourceActorState<TState>, new() where TSnapshot : class where TEvent : class, IEvent
    {
        var history = state.Events.ToArray();
        var previousRevision = Revision(read(state));
        state.AcceptChanges();
        var result = Send(command);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(command.CommandId, result.Value!.Guid);
        var source = Assert.IsType<TEvent>(Assert.Single(state.Events));
        Assert.Equal(command.CommandId, source.CommandId);
        Assert.Equal(command.EntityId.Format(), source.Subject.EntityId);
        Assert.Equal(OperationId, source.GetType().GetProperty("OperationCommandId")!.GetValue(source));
        Assert.True(state.Updated);
        var snapshot = Assert.IsType<TSnapshot>(read(state));
        Assert.Equal(previousRevision + 1, Revision(snapshot));
        then(snapshot);
        var replayed = new TState();
        replayed.ReplayEvents(history.Append(source).Select(RoundTrip));
        Assert.Equivalent(snapshot, read(replayed), strict: true);
        Assert.Empty(replayed.Events);
        Assert.False(replayed.Updated);
    }

    private void Rejected<TState, TSnapshot>(ICommand<ScheduledTaskId> command, TState state,
        Func<TState, TSnapshot?> read, string code)
        where TState : BaseEventSourceActorState<TState> where TSnapshot : class
    {
        state.AcceptChanges();
        var before = read(state);
        var beforeBytes = before is null ? [] : MessagePackSerializer.Serialize(before.GetType(), before);
        var result = Send(command);
        Assert.False(result.Success);
        Assert.Contains(code, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Same(before, read(state));
        if (before is not null)
            Assert.Equal(beforeBytes, MessagePackSerializer.Serialize(before.GetType(), read(state)));
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
    }

    private static long Revision(object? snapshot) => snapshot switch
    {
        ScheduledTaskDefinition d => d.Revision, ScheduledTaskRun r => r.Revision,
        ScheduledTaskCatalog c => c.Revision, null => 0,
        _ => throw new ArgumentException("Unknown scenario snapshot.", nameof(snapshot))
    };
    private sealed class ScenarioClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
