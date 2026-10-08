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
/// <summary>Routes concrete ScheduledTaskRun commands through validation, event-owned state and source persistence.</summary>
public sealed class ScheduledTaskRunCommandActor(ICommandActorContext<ScheduledTaskRunCommandActor> context)
    : BaseEventSourceCommandActor<ScheduledTaskRunCommandActor>(context, ((ScheduledTaskRunCommandContext)context).Logger)
{
    public const string Actor = "ScheduledTaskRunCommand";
    private readonly ScheduledTaskRunCommandContext _owner = context as ScheduledTaskRunCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
    /// <summary>Gets the complete set of routed concrete command types.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes => _receiveMap.Keys.ToArray();
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
            [typeof(RequestScheduledTaskRunCommand)] = command => Validate((RequestScheduledTaskRunCommand)command),
            [typeof(RecordScheduledTaskRunAdmissionCommand)] = command => Validate((RecordScheduledTaskRunAdmissionCommand)command),
            [typeof(RecordScheduledTaskRunStartedCommand)] = command => Validate((RecordScheduledTaskRunStartedCommand)command),
            [typeof(RecordScheduledTaskRunStageCommand)] = command => Validate((RecordScheduledTaskRunStageCommand)command),
            [typeof(CompleteScheduledTaskRunCommand)] = command => Validate((CompleteScheduledTaskRunCommand)command),
            [typeof(FailScheduledTaskRunCommand)] = command => Validate((FailScheduledTaskRunCommand)command),
            [typeof(RecordScheduledTaskRunUncertainCommand)] = command => Validate((RecordScheduledTaskRunUncertainCommand)command),
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, ScheduledTaskRunCommandContext, ScheduledTaskRunCommandState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, ScheduledTaskRunCommandContext, ScheduledTaskRunCommandState, ValueTask<ServiceResult<GuidResult>>>>
    {
            [typeof(RequestScheduledTaskRunCommand)] = (command, owner, state) => ValueTask.FromResult(((RequestScheduledTaskRunCommand)command).Execute(state, owner.Clock.GetUtcNow())),
            [typeof(RecordScheduledTaskRunAdmissionCommand)] = (command, owner, state) => ValueTask.FromResult(((RecordScheduledTaskRunAdmissionCommand)command).Execute(state, owner.Clock.GetUtcNow())),
            [typeof(RecordScheduledTaskRunStartedCommand)] = (command, owner, state) => ValueTask.FromResult(((RecordScheduledTaskRunStartedCommand)command).Execute(state, owner.Clock.GetUtcNow())),
            [typeof(RecordScheduledTaskRunStageCommand)] = (command, owner, state) => ValueTask.FromResult(((RecordScheduledTaskRunStageCommand)command).Execute(state, owner.Clock.GetUtcNow())),
            [typeof(CompleteScheduledTaskRunCommand)] = (command, owner, state) => ValueTask.FromResult(((CompleteScheduledTaskRunCommand)command).Execute(state, owner.Clock.GetUtcNow())),
            [typeof(FailScheduledTaskRunCommand)] = (command, owner, state) => ValueTask.FromResult(((FailScheduledTaskRunCommand)command).Execute(state, owner.Clock.GetUtcNow())),
            [typeof(RecordScheduledTaskRunUncertainCommand)] = (command, owner, state) => ValueTask.FromResult(((RecordScheduledTaskRunUncertainCommand)command).Execute(state, owner.Clock.GetUtcNow())),
    }.ToFrozenDictionary();
    /// <summary>Checks mandatory command and aggregate identities before loading state.</summary>
    private static List<ValidationError> Validate(ICommand<ScheduledTaskId> command)
    {
        var errors = new List<ValidationError>();
        if (command.CommandId == Guid.Empty) errors.Add(new("Command identity is required."));
        if (!command.EntityId.IsValid) errors.Add(new("Scheduled-task aggregate identity is required."));
        return errors;
    }
    /// <inheritdoc />
    protected override ValueTask OnStartup(ICommandActorContext<ScheduledTaskRunCommandActor> context) => _owner.EventProjector.StartAsync(context);
    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<ScheduledTaskRunCommandActor> context) => _owner.EventProjector.StopAsync();
    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<ScheduledTaskRunCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command)
    { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command) => await _owner.Repository.LoadStateAsync(command).ConfigureAwait(false);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) => await _owner.Repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskRunCommandState)state, command);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskRunCommandState)state, command, cancellationToken);
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, IActorState state, ICommand command) => ResolveMappedCommandHandler(command, _receiveMap)(command, _owner, (ScheduledTaskRunCommandState)state);
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ScheduledTaskRunCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, $"ScheduledTaskRun.EXCEPTION;{exception.GetType().Name};{exception.Message}"));
}
