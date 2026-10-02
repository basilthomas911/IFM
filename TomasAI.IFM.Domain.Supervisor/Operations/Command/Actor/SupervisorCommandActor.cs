using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.Validation;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.State;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;

/// <summary>Accepts authorized Supervisor operations and acknowledges command execution only.</summary>
public sealed class SupervisorCommandActor(ICommandActorContext<SupervisorCommandActor> context)
    : BaseEventSourceCommandActor<SupervisorCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = PauseSupervisorActorCommand.Actor;

    protected override ValueTask OnStartup(ICommandActorContext<SupervisorCommandActor> context)
        => Typed(context).EventProjector.StartAsync(context);

    protected override ValueTask OnShutdown(ICommandActorContext<SupervisorCommandActor> context)
        => Typed(context).EventProjector.StopAsync();

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<SupervisorCommandActor> context, ActorThreadId threadId,
        ICommand command, CancellationToken cancellationToken)
        => await Typed(context).StateRepository.LoadStateAsync(command, cancellationToken)
            .ConfigureAwait(false);

    protected override ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<SupervisorCommandActor> context, ActorThreadId threadId,
        ICommand command)
        => LoadStateAsync(context, command, CancellationToken.None);

    static async ValueTask<IActorState> LoadStateAsync(
        ICommandActorContext<SupervisorCommandActor> context, ICommand command,
        CancellationToken cancellationToken)
        => await Typed(context).StateRepository.LoadStateAsync(command, cancellationToken)
            .ConfigureAwait(false);

    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<SupervisorCommandActor> context, ActorThreadId threadId,
        IActorState state, ICommand command, CancellationToken cancellationToken)
        => Typed(context).StateRepository.SaveStateAsync(
            context, (SupervisorCommandState)state, command, cancellationToken);

    protected override ValueTask OnSaveStateAsync(
        ICommandActorContext<SupervisorCommandActor> context, ActorThreadId threadId,
        IActorState state, ICommand command)
        => Typed(context).StateRepository.SaveStateAsync(
            context, (SupervisorCommandState)state, command);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [PauseSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<PauseSupervisorActorCommand>()!,
            [DrainSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<DrainSupervisorActorCommand>()!,
            [ResumeSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<ResumeSupervisorActorCommand>()!,
            [StopSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<StopSupervisorActorCommand>()!,
            [RestartSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<RestartSupervisorActorCommand>()!,
            [QuarantineSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<QuarantineSupervisorActorCommand>()!,
            [RetireSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<RetireSupervisorActorCommand>()!,
            [RecycleSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<RecycleSupervisorActorCommand>()!,
            [AcknowledgeIncidentSupervisorActorCommand.Verb] = static message =>
                message.AsCommand<AcknowledgeIncidentSupervisorActorCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(PauseSupervisorActorCommand)] = static command =>
            {
                var typed = (PauseSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(DrainSupervisorActorCommand)] = static command =>
            {
                var typed = (DrainSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(ResumeSupervisorActorCommand)] = static command =>
            {
                var typed = (ResumeSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(StopSupervisorActorCommand)] = static command =>
            {
                var typed = (StopSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(RestartSupervisorActorCommand)] = static command =>
            {
                var typed = (RestartSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(QuarantineSupervisorActorCommand)] = static command =>
            {
                var typed = (QuarantineSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(RetireSupervisorActorCommand)] = static command =>
            {
                var typed = (RetireSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(RecycleSupervisorActorCommand)] = static command =>
            {
                var typed = (RecycleSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            },
            [typeof(AcknowledgeIncidentSupervisorActorCommand)] = static command =>
            {
                var typed = (AcknowledgeIncidentSupervisorActorCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateEntityId(typed.EntityId, typed.CommandName)
                    .ValidateSupervisorTarget(typed.Target, typed.CommandName)
                    .ValidateSupervisorGeneration(typed.ExpectedGeneration, typed.OperationKind, typed.CommandName)
                    .ValidateSupervisorRequester(typed.Requester, typed.CommandName)
                    .ValidateSupervisorReason(typed.Reason, typed.CommandName)
                    .ValidateSupervisorTimeout(typed.TimeoutTicks, typed.CommandName)
                    .ValidateSupervisorRoute(typed);
            }
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type,
        Func<ICommand, SupervisorCommandState, ISupervisorCommandActorContext, CancellationToken,
            ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, SupervisorCommandState, ISupervisorCommandActorContext, CancellationToken,
            ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(PauseSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((PauseSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(DrainSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((DrainSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(ResumeSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((ResumeSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(StopSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((StopSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(RestartSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((RestartSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(QuarantineSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((QuarantineSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(RetireSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((RetireSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(RecycleSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((RecycleSupervisorActorCommand)command).ExecuteAsync(state, owner, token),
            [typeof(AcknowledgeIncidentSupervisorActorCommand)] = static (command, state, owner, token) =>
                ((AcknowledgeIncidentSupervisorActorCommand)command).ExecuteAsync(state, owner, token)
        }.ToFrozenDictionary();

    protected override ICommand ParseMessage(ICommandActorContext<SupervisorCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(ICommandActorContext<SupervisorCommandActor> context,
        ActorThreadId threadId, ICommand command)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(command);
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<SupervisorCommandActor> context, IActorState state, ICommand command) =>
        ResolveMappedCommandHandler(command, _receiveMap)(
            command, (SupervisorCommandState)state, Typed(context), CancellationToken.None);

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<SupervisorCommandActor> context, IActorState state, ICommand command,
        CancellationToken cancellationToken) =>
        ResolveMappedCommandHandler(command, _receiveMap)(
            command, (SupervisorCommandState)state, Typed(context), cancellationToken);

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<SupervisorCommandActor> context, ActorThreadId threadId,
        ICommand command, Exception exception)
    {
        Typed(context).Logger.LogError(exception,
            "Supervisor command failed for {ActorThreadId}.", threadId);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command?.ErrorCode ?? 9701,
                "Supervisor command processing failed."));
    }

    static ISupervisorCommandActorContext Typed(ICommandActorContext<SupervisorCommandActor> context) =>
        context as ISupervisorCommandActorContext
        ?? throw new ArgumentException("A privileged Supervisor command context is required.", nameof(context));
}
