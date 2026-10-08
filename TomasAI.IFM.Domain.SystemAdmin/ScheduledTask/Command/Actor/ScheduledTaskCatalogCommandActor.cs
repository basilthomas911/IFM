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
/// <summary>Routes concrete ScheduledTaskCatalog commands through validation, event-owned state and source persistence.</summary>
public sealed class ScheduledTaskCatalogCommandActor(ICommandActorContext<ScheduledTaskCatalogCommandActor> context)
    : BaseEventSourceCommandActor<ScheduledTaskCatalogCommandActor>(context, ((ScheduledTaskCatalogCommandContext)context).Logger)
{
    public const string Actor = "ScheduledTaskCatalogCommand";
    private readonly ScheduledTaskCatalogCommandContext _owner = context as ScheduledTaskCatalogCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
    /// <summary>Gets the complete set of routed concrete command types.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes => _receiveMap.Keys.ToArray();
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
            [RegisterScheduledTaskProjectCommand.Verb] = message => message.AsCommand<RegisterScheduledTaskProjectCommand>()!,
            [RecordScheduledTaskHostCapabilityCommand.Verb] = message => message.AsCommand<RecordScheduledTaskHostCapabilityCommand>()!,
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
            [typeof(RegisterScheduledTaskProjectCommand)] = command => Validate((RegisterScheduledTaskProjectCommand)command),
            [typeof(RecordScheduledTaskHostCapabilityCommand)] = command => Validate((RecordScheduledTaskHostCapabilityCommand)command),
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, ScheduledTaskCatalogCommandContext, ScheduledTaskCatalogCommandState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, ScheduledTaskCatalogCommandContext, ScheduledTaskCatalogCommandState, ValueTask<ServiceResult<GuidResult>>>>
    {
            [typeof(RegisterScheduledTaskProjectCommand)] = (command, owner, state) => ValueTask.FromResult(((RegisterScheduledTaskProjectCommand)command).Execute(state, owner.Clock.GetUtcNow())),
            [typeof(RecordScheduledTaskHostCapabilityCommand)] = (command, owner, state) => ValueTask.FromResult(((RecordScheduledTaskHostCapabilityCommand)command).Execute(state, owner.Clock.GetUtcNow())),
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
    protected override ValueTask OnStartup(ICommandActorContext<ScheduledTaskCatalogCommandActor> context) => _owner.EventProjector.StartAsync(context);
    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<ScheduledTaskCatalogCommandActor> context) => _owner.EventProjector.StopAsync();
    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command)
    { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command) => await _owner.Repository.LoadStateAsync(command).ConfigureAwait(false);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) => await _owner.Repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskCatalogCommandState)state, command);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskCatalogCommandState)state, command, cancellationToken);
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, IActorState state, ICommand command) => ResolveMappedCommandHandler(command, _receiveMap)(command, _owner, (ScheduledTaskCatalogCommandState)state);
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command, Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, $"ScheduledTaskCatalog.EXCEPTION;{exception.GetType().Name};{exception.Message}"));
}
