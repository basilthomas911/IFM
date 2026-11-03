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
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
/// <summary>Routes concrete ScheduledTask commands through validation, event-owned state and source persistence.</summary>
public sealed class ScheduledTaskCommandActor(ICommandActorContext<ScheduledTaskCommandActor> context)
    : BaseEventSourceCommandActor<ScheduledTaskCommandActor>(context, ((ScheduledTaskCommandContext)context).Logger)
{
    public const string Actor = "ScheduledTaskCommand";
    private readonly ScheduledTaskCommandContext _owner = context as ScheduledTaskCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
    /// <summary>Gets the complete set of routed concrete command types.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes =>CachedSupportedCommandTypes.Value;
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
            [CreateScheduledTaskCommand.Verb] = message => message.AsCommand<CreateScheduledTaskCommand>()!,
            [ChangeScheduledTaskScheduleCommand.Verb] = message => message.AsCommand<ChangeScheduledTaskScheduleCommand>()!,
            [EnableScheduledTaskCommand.Verb] = message => message.AsCommand<EnableScheduledTaskCommand>()!,
            [DisableScheduledTaskCommand.Verb] = message => message.AsCommand<DisableScheduledTaskCommand>()!,
            [RemoveScheduledTaskCommand.Verb] = message => message.AsCommand<RemoveScheduledTaskCommand>()!,
            [RecordScheduledTaskInstallationCommand.Verb] = message => message.AsCommand<RecordScheduledTaskInstallationCommand>()!,
            [RecordScheduledTaskInstallationFailureCommand.Verb] = message => message.AsCommand<RecordScheduledTaskInstallationFailureCommand>()!,
            [AdmitScheduledTaskRunCommand.Verb] = message => message.AsCommand<AdmitScheduledTaskRunCommand>()!,
            [RecordScheduledTaskRunCompletionCommand.Verb] = message => message.AsCommand<RecordScheduledTaskRunCompletionCommand>()!,
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
            [typeof(CreateScheduledTaskCommand)] = command =>
            {
                var typed = (CreateScheduledTaskCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(ChangeScheduledTaskScheduleCommand)] = command =>
            {
                var typed = (ChangeScheduledTaskScheduleCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(EnableScheduledTaskCommand)] = command =>
            {
                var typed = (EnableScheduledTaskCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(DisableScheduledTaskCommand)] = command =>
            {
                var typed = (DisableScheduledTaskCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RemoveScheduledTaskCommand)] = command =>
            {
                var typed = (RemoveScheduledTaskCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskInstallationCommand)] = command =>
            {
                var typed = (RecordScheduledTaskInstallationCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskInstallationFailureCommand)] = command =>
            {
                var typed = (RecordScheduledTaskInstallationFailureCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(AdmitScheduledTaskRunCommand)] = command =>
            {
                var typed = (AdmitScheduledTaskRunCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskRunCompletionCommand)] = command =>
            {
                var typed = (RecordScheduledTaskRunCompletionCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<ScheduledTaskCommandActor>, ScheduledTaskCommandState, Task<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<ScheduledTaskCommandActor>, ScheduledTaskCommandState, Task<ServiceResult<GuidResult>>>>
    {
            [typeof(CreateScheduledTaskCommand)] = static (command, owner, state) => Task.FromResult(((CreateScheduledTaskCommand)command).Execute(owner, state)),
            [typeof(ChangeScheduledTaskScheduleCommand)] = static (command, owner, state) => Task.FromResult(((ChangeScheduledTaskScheduleCommand)command).Execute(owner, state)),
            [typeof(EnableScheduledTaskCommand)] = static (command, owner, state) => Task.FromResult(((EnableScheduledTaskCommand)command).Execute(owner, state)),
            [typeof(DisableScheduledTaskCommand)] = static (command, owner, state) => Task.FromResult(((DisableScheduledTaskCommand)command).Execute(owner, state)),
            [typeof(RemoveScheduledTaskCommand)] = static (command, owner, state) => Task.FromResult(((RemoveScheduledTaskCommand)command).Execute(owner, state)),
            [typeof(RecordScheduledTaskInstallationCommand)] = static (command, owner, state) => Task.FromResult(((RecordScheduledTaskInstallationCommand)command).Execute(owner, state)),
            [typeof(RecordScheduledTaskInstallationFailureCommand)] = static (command, owner, state) => Task.FromResult(((RecordScheduledTaskInstallationFailureCommand)command).Execute(owner, state)),
            [typeof(AdmitScheduledTaskRunCommand)] = static (command, owner, state) => Task.FromResult(((AdmitScheduledTaskRunCommand)command).Execute(owner, state)),
            [typeof(RecordScheduledTaskRunCompletionCommand)] = static (command, owner, state) => Task.FromResult(((RecordScheduledTaskRunCompletionCommand)command).Execute(owner, state)),
    }.ToFrozenDictionary();
    /// <inheritdoc />
    protected override ValueTask OnStartup(ICommandActorContext<ScheduledTaskCommandActor> context) => _owner.EventProjector.StartAsync(context);
    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<ScheduledTaskCommandActor> context) => _owner.EventProjector.StopAsync();
    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<ScheduledTaskCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<ScheduledTaskCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(threadId);
        ArgumentNullException.ThrowIfNull(command);
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskCommandActor> context, ActorThreadId threadId, ICommand command) => await _owner.Repository.LoadStateAsync(command).ConfigureAwait(false);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) => await _owner.Repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskCommandState)state, command);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskCommandState)state, command, cancellationToken);
    /// <inheritdoc />
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ScheduledTaskCommandActor> context, IActorState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        return await ResolveMappedCommandHandler(command, _receiveMap)(command, context, (ScheduledTaskCommandState)state).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ScheduledTaskCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) 
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, $"ScheduledTask.EXCEPTION;{ex.GetType().Name};{ex.Message}"));
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedCommandTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
