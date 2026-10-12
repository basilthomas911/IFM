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

public sealed class ParameterAssignmentCommandActor(ICommandActorContext<ParameterAssignmentCommandActor> context)
 : BaseEventSourceCommandActor<ParameterAssignmentCommandActor>(context, ((IParameterAssignmentCommandContext)context).Logger)
{
    readonly ConcurrentDictionary<(string Stream, Guid Command), IAsyncDisposable> leases = new();
    public const string ActorName = "ParameterAssignmentCommand";
    IParameterAssignmentCommandContext Services { get; } = (IParameterAssignmentCommandContext)context;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [AssignParameterVersionCommand.Verb] = m => m.AsCommand<AssignParameterVersionCommand>()!,
        [DisableParameterAssignmentCommand.Verb] = m => m.AsCommand<DisableParameterAssignmentCommand>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(AssignParameterVersionCommand)] = static command =>
        {
            var typed = (AssignParameterVersionCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterAssignmentId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },        [typeof(DisableParameterAssignmentCommand)] = static command =>
        {
            var typed = (DisableParameterAssignmentCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterAssignmentId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },    };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, IParameterAssignmentCommandContext, ParameterAssignmentCommandState, Task<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, IParameterAssignmentCommandContext, ParameterAssignmentCommandState, Task<ServiceResult<GuidResult>>>>
    {
        [typeof(AssignParameterVersionCommand)] = (c, ctx, state) => ((AssignParameterVersionCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
        [typeof(DisableParameterAssignmentCommand)] = (c, ctx, state) => ((DisableParameterAssignmentCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
    };
    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<ParameterAssignmentCommandActor> context, ICommand command, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    protected override ValueTask OnCommandFinishedAsync(ICommandActorContext<ParameterAssignmentCommandActor> context, ICommand? command) => command is null ? ValueTask.CompletedTask : ReleaseLease(command);
    protected override ICommand ParseMessage(ICommandActorContext<ParameterAssignmentCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<ParameterAssignmentCommandActor> context, ActorThreadId threadId, ICommand command) { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ParameterAssignmentCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var lease = await (Services.ConfigurationDb.AcquireParameterWriteLeaseAsync()).ConfigureAwait(false);
        if (!leases.TryAdd((command.StreamId, command.CommandId), lease)) { await (lease.DisposeAsync()).ConfigureAwait(false); throw new InvalidOperationException("PARAM.OPERATION_ALREADY_RUNNING"); }
        try { return await (Services.StateRepository.LoadStateAsync(command)).ConfigureAwait(false); }
        catch { await (ReleaseLease(command)).ConfigureAwait(false); throw; }
    }
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<ParameterAssignmentCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
    { try { await (Services.StateRepository.SaveStateAsync(context, (ParameterAssignmentCommandState)state, command)).ConfigureAwait(false); } finally { await (ReleaseLease(command)).ConfigureAwait(false); } }
    async ValueTask ReleaseLease(ICommand? cmd) { if (cmd is not null && leases.TryRemove((cmd.StreamId, cmd.CommandId), out var lease)) await (lease.DisposeAsync()).ConfigureAwait(false); }
    protected override async ValueTask OnStartup(ICommandActorContext<ParameterAssignmentCommandActor> context) => await (Services.EventProjector.StartAsync(context)).ConfigureAwait(false);
    protected override async ValueTask OnShutdown(ICommandActorContext<ParameterAssignmentCommandActor> context) => await (Services.EventProjector.StopAsync()).ConfigureAwait(false);
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ParameterAssignmentCommandActor> context, IActorState state, ICommand command) => await (ResolveMappedCommandHandler(command, _receiveMap)(command, Services, (ParameterAssignmentCommandState)state)).ConfigureAwait(false);
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ParameterAssignmentCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) {
        ArgumentNullException.ThrowIfNull(ex); await (ReleaseLease(command)).ConfigureAwait(false); return new ServiceFailed<GuidResult>(command?.ErrorCode ?? 33010, ex.Message); }
}
