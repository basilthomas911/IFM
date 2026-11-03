using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Validation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Validation;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
/// <summary>Routes concrete ScheduledTaskRun commands through validation, event-owned state and source persistence.</summary>
public sealed class ScheduledTaskRunCommandActor(ICommandActorContext<ScheduledTaskRunCommandActor> context)
    : BaseEventSourceCommandActor<ScheduledTaskRunCommandActor>(context, ((ScheduledTaskRunCommandContext)context).Logger)
{
    public const string Actor = "ScheduledTaskRunCommand";
    private readonly ScheduledTaskRunCommandContext _owner = context as ScheduledTaskRunCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
    /// <summary>Gets the complete set of routed concrete command types.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes =>CachedSupportedCommandTypes.Value;
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
            [RequestScheduledTaskRunCommand.Verb] = message => message.AsCommand<RequestScheduledTaskRunCommand>()!,
            [RecordScheduledTaskRunAdmissionCommand.Verb] = message => message.AsCommand<RecordScheduledTaskRunAdmissionCommand>()!,
            [RecordScheduledTaskRunStartedCommand.Verb] = message => message.AsCommand<RecordScheduledTaskRunStartedCommand>()!,
            [RecordScheduledTaskRunStageCommand.Verb] = message => message.AsCommand<RecordScheduledTaskRunStageCommand>()!,
            [CompleteScheduledTaskRunCommand.Verb] = message => message.AsCommand<CompleteScheduledTaskRunCommand>()!,
            [FailScheduledTaskRunCommand.Verb] = message => message.AsCommand<FailScheduledTaskRunCommand>()!,
            [RecordScheduledTaskRunUncertainCommand.Verb] = message => message.AsCommand<RecordScheduledTaskRunUncertainCommand>()!,
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
            [typeof(RequestScheduledTaskRunCommand)] = command =>
            {
                var typed = (RequestScheduledTaskRunCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskRunAdmissionCommand)] = command =>
            {
                var typed = (RecordScheduledTaskRunAdmissionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskRunStartedCommand)] = command =>
            {
                var typed = (RecordScheduledTaskRunStartedCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskRunStageCommand)] = command =>
            {
                var typed = (RecordScheduledTaskRunStageCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(CompleteScheduledTaskRunCommand)] = command =>
            {
                var typed = (CompleteScheduledTaskRunCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(FailScheduledTaskRunCommand)] = command =>
            {
                var typed = (FailScheduledTaskRunCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskRunUncertainCommand)] = command =>
            {
                var typed = (RecordScheduledTaskRunUncertainCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<ScheduledTaskRunCommandActor>, ScheduledTaskRunCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<ScheduledTaskRunCommandActor>, ScheduledTaskRunCommandState, ServiceResult<GuidResult>>>
    {
            [typeof(RequestScheduledTaskRunCommand)] = static (command, owner, state) => ((RequestScheduledTaskRunCommand)command).Execute(owner, state),
            [typeof(RecordScheduledTaskRunAdmissionCommand)] = static (command, owner, state) => ((RecordScheduledTaskRunAdmissionCommand)command).Execute(owner, state),
            [typeof(RecordScheduledTaskRunStartedCommand)] = static (command, owner, state) => ((RecordScheduledTaskRunStartedCommand)command).Execute(owner, state),
            [typeof(RecordScheduledTaskRunStageCommand)] = static (command, owner, state) => ((RecordScheduledTaskRunStageCommand)command).Execute(owner, state),
            [typeof(CompleteScheduledTaskRunCommand)] = static (command, owner, state) => ((CompleteScheduledTaskRunCommand)command).Execute(owner, state),
            [typeof(FailScheduledTaskRunCommand)] = static (command, owner, state) => ((FailScheduledTaskRunCommand)command).Execute(owner, state),
            [typeof(RecordScheduledTaskRunUncertainCommand)] = static (command, owner, state) => ((RecordScheduledTaskRunUncertainCommand)command).Execute(owner, state),
    }.ToFrozenDictionary();
    /// <inheritdoc />
    protected override ValueTask OnStartup(ICommandActorContext<ScheduledTaskRunCommandActor> context) => _owner.EventProjector.StartAsync(context);
    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<ScheduledTaskRunCommandActor> context) => _owner.EventProjector.StopAsync();
    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<ScheduledTaskRunCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(threadId);
        ArgumentNullException.ThrowIfNull(command);
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command) => await _owner.Repository.LoadStateAsync(command).ConfigureAwait(false);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) => await _owner.Repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskRunCommandState)state, command);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskRunCommandState)state, command, cancellationToken);
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, IActorState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        return ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, context, (ScheduledTaskRunCommandState)state));
    }
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) 
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, $"ScheduledTaskRun.EXCEPTION;{ex.GetType().Name};{ex.Message}"));
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedCommandTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
