using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Validation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Commands;
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
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
/// <summary>Routes concrete ScheduledTaskCatalog commands through validation, event-owned state and source persistence.</summary>
public sealed class ScheduledTaskCatalogCommandActor(ICommandActorContext<ScheduledTaskCatalogCommandActor> context)
    : BaseEventSourceCommandActor<ScheduledTaskCatalogCommandActor>(context, ((ScheduledTaskCatalogCommandContext)context).Logger)
{
    public const string Actor = "ScheduledTaskCatalogCommand";
    private readonly ScheduledTaskCatalogCommandContext _owner = context as ScheduledTaskCatalogCommandContext ?? throw new ArgumentException("Typed scheduled-task context required.", nameof(context));
    /// <summary>Gets the complete set of routed concrete command types.</summary>
    public static IReadOnlyCollection<Type> SupportedCommandTypes =>CachedSupportedCommandTypes.Value;
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
            [RegisterScheduledTaskProjectCommand.Verb] = message => message.AsCommand<RegisterScheduledTaskProjectCommand>()!,
            [RecordScheduledTaskHostCapabilityCommand.Verb] = message => message.AsCommand<RecordScheduledTaskHostCapabilityCommand>()!,
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
            [typeof(RegisterScheduledTaskProjectCommand)] = command =>
            {
                var typed = (RegisterScheduledTaskProjectCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
            [typeof(RecordScheduledTaskHostCapabilityCommand)] = command =>
            {
                var typed = (RecordScheduledTaskHostCapabilityCommand)command;
                return new List<ValidationError>()
                    .ValidateCommandId(typed.CommandId, typed.CommandName)
                    .ValidateScheduledTaskId(typed.EntityId, typed.CommandName)
                    .ValidateScheduledTaskCommand(typed);
            },
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<ScheduledTaskCatalogCommandActor>, ScheduledTaskCatalogCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<ScheduledTaskCatalogCommandActor>, ScheduledTaskCatalogCommandState, ServiceResult<GuidResult>>>
    {
            [typeof(RegisterScheduledTaskProjectCommand)] = static (command, owner, state) => ((RegisterScheduledTaskProjectCommand)command).Execute(owner, state),
            [typeof(RecordScheduledTaskHostCapabilityCommand)] = static (command, owner, state) => ((RecordScheduledTaskHostCapabilityCommand)command).Execute(owner, state),
    }.ToFrozenDictionary();
    /// <inheritdoc />
    protected override ValueTask OnStartup(ICommandActorContext<ScheduledTaskCatalogCommandActor> context) => _owner.EventProjector.StartAsync(context);
    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<ScheduledTaskCatalogCommandActor> context) => _owner.EventProjector.StopAsync();
    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(threadId);
        ArgumentNullException.ThrowIfNull(command);
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command) => await _owner.Repository.LoadStateAsync(command).ConfigureAwait(false);
    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken cancellationToken) => await _owner.Repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskCatalogCommandState)state, command);
    /// <inheritdoc />
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken cancellationToken) => _owner.Repository.SaveStateAsync(context, (ScheduledTaskCatalogCommandState)state, command, cancellationToken);
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, IActorState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        return ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, context, (ScheduledTaskCatalogCommandState)state));
    }
    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ScheduledTaskCatalogCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) 
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, $"ScheduledTaskCatalog.EXCEPTION;{ex.GetType().Name};{ex.Message}"));
    }
private static readonly System.Lazy<IReadOnlyCollection<Type>> CachedSupportedCommandTypes = new(() => System.Array.AsReadOnly(_receiveMap.Keys.ToArray()), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
}
