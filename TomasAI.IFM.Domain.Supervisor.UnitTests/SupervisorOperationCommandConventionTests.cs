using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Supervisor.Operations.Command;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.State;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Operations.Event;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.State;
using TomasAI.IFM.Domain.Supervisor.Recovery.Command.Validation;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorOperationCommandConventionTests
{
    static readonly ActorThreadId Target = new(ActorType.Command, "ExampleCommand", "entity-42");

    [Fact]
    public void Every_operation_has_exact_parse_validation_and_receive_map_entries()
    {
        var actor = typeof(SupervisorCommandActor);
        var parse = (IReadOnlyDictionary<string, Func<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorMessage,
            ICommand>>)actor.GetField("_parseMap", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var validation = (IReadOnlyDictionary<Type, Func<ICommand,
            List<ValidationError>>>)actor.GetField("_validationMap",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var receiveMap = actor.GetField("_receiveMap", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var receiveTypes = (IEnumerable<Type>)receiveMap.GetType()
            .GetProperty("Keys")!.GetValue(receiveMap)!;
        Assert.Equal(9, parse.Count);
        Assert.Equal(9, validation.Count);
        Assert.Equal(validation.Keys.OrderBy(type => type.Name),
            receiveTypes.OrderBy(type => type.Name));
        Assert.DoesNotContain("ExecuteActorOperation", parse.Keys);
    }

    [Fact]
    public void Every_terminal_event_has_exact_parse_and_receive_map_entries()
    {
        var actor = typeof(SupervisorEventActor);
        var parseMap = actor.GetField("_parseMap", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var receiveMap = actor.GetField("_receiveMap", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
        var parseKeys = (System.Collections.ICollection)parseMap.GetType()
            .GetProperty("Keys")!.GetValue(parseMap)!;
        var receiveKeys = (System.Collections.ICollection)receiveMap.GetType()
            .GetProperty("Keys")!.GetValue(receiveMap)!;
        Assert.Equal(18, parseKeys.Count);
        Assert.Equal(18, receiveKeys.Count);
    }

    [Fact]
    public void Supervisor_command_and_terminal_event_round_trip_their_permanent_wire_fields()
    {
        var command = new PauseSupervisorActorCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, PauseSupervisorActorCommand.Actor,
                PauseSupervisorActorCommand.Verb, ActorEntityId.Default.Format()),
            Target = Target,
            ExpectedGeneration = 7,
            Requester = "operator",
            Reason = "qualification",
            TimeoutTicks = TimeSpan.FromMinutes(1).Ticks
        };
        var decoded = MessagePackSerializer.Deserialize<PauseSupervisorActorCommand>(
            MessagePackSerializer.Serialize(command, MessagePackBinarySerializer.Options),
            MessagePackBinarySerializer.Options);
        Assert.Equal(command.CommandId, decoded.CommandId);
        Assert.Equal(command.Target, decoded.Target);
        Assert.True(decoded.PostEvents);
        Assert.Equal(PauseSupervisorActorCommand.ErrorId, decoded.ErrorCode);
        Assert.Equal(BoundedContextName.SupervisorBoundedContext, decoded.RouteTo);

        var terminal = new PauseSupervisorActorCompleteEvent
        {
            CommandId = command.CommandId,
            Target = Target,
            ExpectedGeneration = 7,
            Requester = "operator",
            Reason = "qualification",
            Stage = "Completed",
            TimeoutTicks = command.TimeoutTicks
        };
        var decodedTerminal = MessagePackSerializer.Deserialize<PauseSupervisorActorCompleteEvent>(
            MessagePackSerializer.Serialize(terminal, MessagePackBinarySerializer.Options),
            MessagePackBinarySerializer.Options);
        Assert.Equal(command.CommandId, decodedTerminal.CommandId);
        Assert.Equal(Target, decodedTerminal.Target);
        Assert.Equal("Completed", decodedTerminal.Stage);
    }

    [Fact]
    public async Task Recovery_canary_validates_correlation_route_and_applies_private_source_event()
    {
        var correlationId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var command = new RecoveryCanaryCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, RecoveryCanaryCommand.Actor,
                RecoveryCanaryCommand.Verb, correlationId.ToString("N")),
            CorrelationId = correlationId,
            GenerationId = generationId,
            ValueDate = new DateOnly(2026, 9, 30),
            Dataset = "GLBX.MDP3",
            IssuedUtc = DateTime.UtcNow
        };
        Assert.Empty(new List<ValidationError>().ValidateCanaryRoute(command));
        Assert.Single(new List<ValidationError>().ValidateCanaryRoute(command with
        {
            Subject = new(ActorType.Command, RecoveryCanaryCommand.Actor,
                RecoveryCanaryCommand.Verb, "wrong")
        }));

        var state = new RecoveryCanaryCommandState();
        var result = await command.ExecuteAsync(state, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(command.CommandId, result.Value!.Guid);
        Assert.Equal(correlationId, state.LastCorrelationId);
        var source = Assert.IsType<RecoveryCanaryRecordedEvent>(Assert.Single(state.Events));
        Assert.Equal(generationId, source.GenerationId);
        Assert.Equal(EventType.DomainEvent, source.EventType);
    }

    [Fact]
    public void Validation_collects_independent_errors_before_execution()
    {
        var command = new RestartSupervisorActorCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, RestartSupervisorActorCommand.Actor,
                RestartSupervisorActorCommand.Verb, "wrong"),
            EntityId = ActorEntityId.Default,
            Target = default,
            ExpectedGeneration = 0,
            Requester = string.Empty,
            Reason = string.Empty,
            TimeoutTicks = 0
        };
        var map = (IReadOnlyDictionary<Type, Func<ICommand,
            List<ValidationError>>>)typeof(SupervisorCommandActor)
            .GetField("_validationMap", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;

        var errors = map[typeof(RestartSupervisorActorCommand)](command);

        Assert.True(errors.Count >= 6);
    }

    [Fact]
    public async Task Successful_operation_applies_source_event_and_returns_only_command_acknowledgement()
    {
        var commandId = Guid.NewGuid();
        var command = new PauseSupervisorActorCommand
        {
            CommandId = commandId,
            Subject = new(ActorType.Command, PauseSupervisorActorCommand.Actor,
                PauseSupervisorActorCommand.Verb, ActorEntityId.Default.Format()),
            EntityId = ActorEntityId.Default,
            Target = Target,
            ExpectedGeneration = 7,
            Requester = "operator",
            Reason = "qualification",
            TimeoutTicks = TimeSpan.FromMinutes(1).Ticks
        };
        var context = Substitute.For<ISupervisorCommandActorContext>();
        context.Authorizer.IsAuthorized("operator", SupervisorActorOperationKind.Pause).Returns(true);
        context.ManagedActors.PauseAsync(Arg.Any<SupervisorActorOperationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(new SupervisorActorOperationResult(
                SupervisorOperationOutcome.Succeeded, commandId, Target, 7, "Completed", null)));
        var state = new SupervisorCommandState();

        var result = await command.ExecuteAsync(state, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(commandId, result.Value!.Guid);
        Assert.Equal(commandId, state.LastOperationId);
        var source = Assert.IsType<SupervisorActorOperationRecordedEvent>(Assert.Single(state.Events));
        Assert.Equal(SupervisorActorOperationKind.Pause, source.Operation);
        Assert.Equal(SupervisorOperationOutcome.Succeeded, source.Outcome);
        await context.ManagedActors.Received(1).PauseAsync(
            Arg.Is<SupervisorActorOperationRequest>(request =>
                request.Operation == SupervisorActorOperationKind.Pause
                && request.ExpectedGeneration == 7), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SupervisorActorOperationKind.Pause)]
    [InlineData(SupervisorActorOperationKind.Drain)]
    [InlineData(SupervisorActorOperationKind.Resume)]
    [InlineData(SupervisorActorOperationKind.Stop)]
    [InlineData(SupervisorActorOperationKind.Restart)]
    [InlineData(SupervisorActorOperationKind.Quarantine)]
    [InlineData(SupervisorActorOperationKind.Retire)]
    [InlineData(SupervisorActorOperationKind.Recycle)]
    public async Task Concrete_handler_calls_only_its_named_lifecycle_capability(
        SupervisorActorOperationKind operation)
    {
        var command = CreateOperationCommand(operation);
        var context = Substitute.For<ISupervisorCommandActorContext>();
        context.Authorizer.IsAuthorized("operator", operation).Returns(true);
        var expected = new SupervisorActorOperationResult(SupervisorOperationOutcome.Succeeded,
            command.CommandId, Target, 7, "Completed", null);
        ConfigureLifecycle(context.ManagedActors, expected);
        var state = new SupervisorCommandState();

        var result = await ExecuteOperationAsync(command, state, context);

        Assert.True(result.Success);
        Assert.Equal(operation,
            Assert.IsType<SupervisorActorOperationRecordedEvent>(Assert.Single(state.Events)).Operation);
        await CallNamedLifecycleAsync(context.ManagedActors.Received(1), operation,
            Arg.Is<SupervisorActorOperationRequest>(request =>
                request.Operation == operation && request.OperationId == command.CommandId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unauthorized_operation_records_rejection_without_calling_lifecycle()
    {
        var command = CreateOperationCommand(SupervisorActorOperationKind.Restart);
        var context = Substitute.For<ISupervisorCommandActorContext>();
        var state = new SupervisorCommandState();

        var result = await ExecuteOperationAsync(command, state, context);

        Assert.False(result.Success);
        var source = Assert.IsType<SupervisorActorOperationRecordedEvent>(Assert.Single(state.Events));
        Assert.Equal(SupervisorOperationOutcome.Rejected, source.Outcome);
        Assert.Equal("Authorization", source.Stage);
        await context.ManagedActors.DidNotReceiveWithAnyArgs()
            .RestartAsync(default!, default);
    }

    [Fact]
    public async Task Acknowledge_incident_records_outcome_without_mutating_incident_before_commit()
    {
        var command = new AcknowledgeIncidentSupervisorActorCommand(Guid.NewGuid(),
            Subject(AcknowledgeIncidentSupervisorActorCommand.Verb), Target, 7,
            "operator", "reviewed", TimeSpan.FromSeconds(30).Ticks);
        var context = Substitute.For<ISupervisorCommandActorContext>();
        context.Authorizer.IsAuthorized("operator", SupervisorActorOperationKind.AcknowledgeIncident)
            .Returns(true);
        var incidentStore = Substitute.For<ISupervisorIncidentStore>();
        context.Incidents.Returns(incidentStore);
        incidentStore.ActiveIncidents.Returns([new SupervisorActorIncident(Target,
            SupervisorActorHealth.Degraded, DateTime.UtcNow, DateTime.UtcNow, 7, 1, 1)]);
        var state = new SupervisorCommandState();

        var result = await command.ExecuteAsync(state, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(SupervisorActorOperationKind.AcknowledgeIncident,
            Assert.IsType<SupervisorActorOperationRecordedEvent>(Assert.Single(state.Events)).Operation);
        incidentStore.DidNotReceiveWithAnyArgs().Acknowledge(default, default!, default!);
        Assert.Empty(context.ManagedActors.ReceivedCalls());
    }

    [Fact]
    public async Task Acknowledge_complete_event_projects_incident_removal()
    {
        var context = Substitute.For<ISupervisorEventActorContext>();
        var operationStore = new TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.SupervisorOperationStore();
        context.Operations.Returns(operationStore);
        var completed = new AcknowledgeIncidentSupervisorActorCompleteEvent
        {
            CommandId = Guid.NewGuid(), Target = Target, ExpectedGeneration = 7,
            Requester = "operator", Reason = "reviewed", Stage = "IncidentAcknowledgement",
            TimeoutTicks = TimeSpan.FromSeconds(30).Ticks
        };

        await completed.ExecuteAsync(context, NullLogger<SupervisorEventActor>.Instance);

        context.Incidents.Received(1).Acknowledge(Target, "operator", "reviewed");
        Assert.Single(operationStore.RecentOperations);
    }

    static ActorSubject Subject(string verb) => new(ActorType.Command,
        PauseSupervisorActorCommand.Actor, verb, ActorEntityId.Default.Format());

    static ISupervisorOperationCommand CreateOperationCommand(SupervisorActorOperationKind operation)
    {
        var id = Guid.NewGuid();
        const long generation = 7;
        const string requester = "operator";
        const string reason = "qualification";
        var timeout = TimeSpan.FromSeconds(30).Ticks;
        return operation switch
        {
            SupervisorActorOperationKind.Pause => new PauseSupervisorActorCommand(id,
                Subject(PauseSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            SupervisorActorOperationKind.Drain => new DrainSupervisorActorCommand(id,
                Subject(DrainSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            SupervisorActorOperationKind.Resume => new ResumeSupervisorActorCommand(id,
                Subject(ResumeSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            SupervisorActorOperationKind.Stop => new StopSupervisorActorCommand(id,
                Subject(StopSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            SupervisorActorOperationKind.Restart => new RestartSupervisorActorCommand(id,
                Subject(RestartSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            SupervisorActorOperationKind.Quarantine => new QuarantineSupervisorActorCommand(id,
                Subject(QuarantineSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            SupervisorActorOperationKind.Retire => new RetireSupervisorActorCommand(id,
                Subject(RetireSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            SupervisorActorOperationKind.Recycle => new RecycleSupervisorActorCommand(id,
                Subject(RecycleSupervisorActorCommand.Verb), Target, generation, requester, reason, timeout),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    static ValueTask<ServiceResult<GuidResult>> ExecuteOperationAsync(ISupervisorOperationCommand command,
        SupervisorCommandState state, ISupervisorCommandActorContext context) => command switch
    {
        PauseSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        DrainSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        ResumeSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        StopSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        RestartSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        QuarantineSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        RetireSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        RecycleSupervisorActorCommand value => value.ExecuteAsync(state, context, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    static void ConfigureLifecycle(ISupervisorManagedActorLifecycle lifecycle,
        SupervisorActorOperationResult result)
    {
        lifecycle.PauseAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
        lifecycle.DrainAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
        lifecycle.ResumeAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
        lifecycle.StopAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
        lifecycle.RestartAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
        lifecycle.QuarantineAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
        lifecycle.RetireAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
        lifecycle.RecycleAsync(Arg.Any<SupervisorActorOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(result));
    }

    static ValueTask<SupervisorActorOperationResult> CallNamedLifecycleAsync(
        ISupervisorManagedActorLifecycle lifecycle, SupervisorActorOperationKind operation,
        SupervisorActorOperationRequest request, CancellationToken cancellationToken) => operation switch
    {
        SupervisorActorOperationKind.Pause => lifecycle.PauseAsync(request, cancellationToken),
        SupervisorActorOperationKind.Drain => lifecycle.DrainAsync(request, cancellationToken),
        SupervisorActorOperationKind.Resume => lifecycle.ResumeAsync(request, cancellationToken),
        SupervisorActorOperationKind.Stop => lifecycle.StopAsync(request, cancellationToken),
        SupervisorActorOperationKind.Restart => lifecycle.RestartAsync(request, cancellationToken),
        SupervisorActorOperationKind.Quarantine => lifecycle.QuarantineAsync(request, cancellationToken),
        SupervisorActorOperationKind.Retire => lifecycle.RetireAsync(request, cancellationToken),
        SupervisorActorOperationKind.Recycle => lifecycle.RecycleAsync(request, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
