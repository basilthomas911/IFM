using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Validation;
using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Domain;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;

public sealed class ParameterSetCommandActor(ICommandActorContext<ParameterSetCommandActor> context)
 : BaseEventSourceCommandActor<ParameterSetCommandActor>(context, ((IParameterSetCommandContext)context).Logger)
{
    readonly ConcurrentDictionary<(string Stream, Guid Command), IAsyncDisposable> leases = new();
    public const string ActorName = "ParameterSetCommand";
    IParameterSetCommandContext Services { get; } = (IParameterSetCommandContext)context;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [CreateParameterSetCommand.Verb] = m => m.AsCommand<CreateParameterSetCommand>()!,
        [SaveParameterDraftCommand.Verb] = m => m.AsCommand<SaveParameterDraftCommand>()!,
        [RenameParameterSetCommand.Verb] = m => m.AsCommand<RenameParameterSetCommand>()!,
        [PublishParameterVersionCommand.Verb] = m => m.AsCommand<PublishParameterVersionCommand>()!,
        [RetireParameterVersionCommand.Verb] = m => m.AsCommand<RetireParameterVersionCommand>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(CreateParameterSetCommand)] = static command =>
        {
            var typed = (CreateParameterSetCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterSetId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },        [typeof(SaveParameterDraftCommand)] = static command =>
        {
            var typed = (SaveParameterDraftCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterSetId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },        [typeof(RenameParameterSetCommand)] = static command =>
        {
            var typed = (RenameParameterSetCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterSetId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },        [typeof(PublishParameterVersionCommand)] = static command =>
        {
            var typed = (PublishParameterVersionCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterSetId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },        [typeof(RetireParameterVersionCommand)] = static command =>
        {
            var typed = (RetireParameterVersionCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterSetId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },    };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, IParameterSetCommandContext, ParameterSetCommandState, Task<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, IParameterSetCommandContext, ParameterSetCommandState, Task<ServiceResult<GuidResult>>>>
    {
        [typeof(CreateParameterSetCommand)] = (c, ctx, state) => ((CreateParameterSetCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
        [typeof(SaveParameterDraftCommand)] = (c, ctx, state) => ((SaveParameterDraftCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
        [typeof(RenameParameterSetCommand)] = (c, ctx, state) => ((RenameParameterSetCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
        [typeof(PublishParameterVersionCommand)] = (c, ctx, state) => ((PublishParameterVersionCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
        [typeof(RetireParameterVersionCommand)] = (c, ctx, state) => ((RetireParameterVersionCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
    };
    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<ParameterSetCommandActor> context, ICommand command, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    protected override ValueTask OnCommandFinishedAsync(ICommandActorContext<ParameterSetCommandActor> context, ICommand? command) => command is null ? ValueTask.CompletedTask : ReleaseLease(command);
    protected override ICommand ParseMessage(ICommandActorContext<ParameterSetCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<ParameterSetCommandActor> context, ActorThreadId threadId, ICommand command) { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ParameterSetCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command is CreateParameterSetCommand or SaveParameterDraftCommand)
        {
            var mutation = (IParameterSetMutation)command;
            if (await (Services.ConfigurationDb.GetParameterSchemaAsync(mutation.ComponentCode, mutation.SchemaVersion)).ConfigureAwait(false) is null)
                throw new InvalidOperationException("PARAM.SCHEMA_UNREGISTERED");
        }
        var lease = await (Services.ConfigurationDb.AcquireParameterWriteLeaseAsync()).ConfigureAwait(false);
        if (!leases.TryAdd((command.StreamId, command.CommandId), lease)) { await (lease.DisposeAsync()).ConfigureAwait(false); throw new InvalidOperationException("PARAM.OPERATION_ALREADY_RUNNING"); }
        try { return await (Services.StateRepository.LoadStateAsync(command)).ConfigureAwait(false); }
        catch { await (ReleaseLease(command)).ConfigureAwait(false); throw; }
    }
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<ParameterSetCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
    { try { await (Services.StateRepository.SaveStateAsync(context, (ParameterSetCommandState)state, command)).ConfigureAwait(false); } finally { await (ReleaseLease(command)).ConfigureAwait(false); } }
    async ValueTask ReleaseLease(ICommand? cmd) { if (cmd is not null && leases.TryRemove((cmd.StreamId, cmd.CommandId), out var lease)) await (lease.DisposeAsync()).ConfigureAwait(false); }
    protected override async ValueTask OnStartup(ICommandActorContext<ParameterSetCommandActor> context) => await (Services.EventProjector.StartAsync(context)).ConfigureAwait(false);
    protected override async ValueTask OnShutdown(ICommandActorContext<ParameterSetCommandActor> context) => await (Services.EventProjector.StopAsync()).ConfigureAwait(false);
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ParameterSetCommandActor> context, IActorState state, ICommand command) => await (ResolveMappedCommandHandler(command, _receiveMap)(command, Services, (ParameterSetCommandState)state)).ConfigureAwait(false);
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ParameterSetCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) {
        ArgumentNullException.ThrowIfNull(ex); await (ReleaseLease(command)).ConfigureAwait(false); return new ServiceFailed<GuidResult>(command?.ErrorCode ?? 33010, ex.Message); }
}
