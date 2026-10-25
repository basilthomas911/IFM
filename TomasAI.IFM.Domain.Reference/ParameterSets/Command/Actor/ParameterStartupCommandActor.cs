using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Validation;
using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Application.CommandPipeline;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;

public sealed class ParameterStartupCommandActor(ICommandActorContext<ParameterStartupCommandActor> context)
 : BaseEventSourceCommandActor<ParameterStartupCommandActor>(context, ((IParameterStartupCommandContext)context).Logger)
{
    readonly ConcurrentDictionary<(string Stream, Guid Command), IAsyncDisposable> leases = new();
    public const string ActorName = "ParameterStartupCommand";
    IParameterStartupCommandContext Services { get; } = (IParameterStartupCommandContext)context;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [ApplySignalStartupPlanCommand.Verb] = m => m.AsCommand<ApplySignalStartupPlanCommand>()!,
        [ReleaseSignalStartupPlanCommand.Verb] = m => m.AsCommand<ReleaseSignalStartupPlanCommand>()!,
        [RecordSignalStartupReportCommand.Verb] = m => m.AsCommand<RecordSignalStartupReportCommand>()!,
    };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(ApplySignalStartupPlanCommand)] = static command =>
        {
            var typed = (ApplySignalStartupPlanCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterStartupId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },        [typeof(ReleaseSignalStartupPlanCommand)] = static command =>
        {
            var typed = (ReleaseSignalStartupPlanCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterStartupId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },        [typeof(RecordSignalStartupReportCommand)] = static command =>
        {
            var typed = (RecordSignalStartupReportCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateParameterStartupId(typed.EntityId)
                .ValidateParameterCommand(typed);
        },    };
    static readonly IReadOnlyDictionary<Type, Func<ICommand, IParameterStartupCommandContext, ParameterStartupCommandState, Task<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, IParameterStartupCommandContext, ParameterStartupCommandState, Task<ServiceResult<GuidResult>>>>
    {
        [typeof(ApplySignalStartupPlanCommand)] = (c, ctx, state) => ((ApplySignalStartupPlanCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
        [typeof(ReleaseSignalStartupPlanCommand)] = (c, ctx, state) => ((ReleaseSignalStartupPlanCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
        [typeof(RecordSignalStartupReportCommand)] = (c, ctx, state) => ((RecordSignalStartupReportCommand)c).ExecuteAsync(ctx, state, ctx.Logger),
    };
    protected override async ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<ParameterStartupCommandActor> context, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var previous = await (Services.DbEventSource.GetCommandLogAsync(command.CommandId).WaitAsync(cancellationToken)).ConfigureAwait(false)
          ?? throw new InvalidOperationException("PARAM.OPERATION_RESERVATION_NOT_READY");
        if (previous.CommandPayloadSha256 is { Length: > 0 })
        {
            var codec = new TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditMessagePackCodec();
            if (!string.Equals(previous.CommandName,command.CommandName,StringComparison.Ordinal) || !string.Equals(previous.StreamId,command.StreamId,StringComparison.Ordinal) ||
               previous.CommandPayloadFormat != (short)TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditPayloadFormat.MessagePack ||
               previous.CommandPayloadVersion != TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit.CommandAuditMessagePackCodec.CurrentVersion ||
               !codec.Matches(command, previous.CommandPayloadSha256))
                throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
        }
        else
            TomasAI.IFM.Domain.Reference.ParameterSets.Model.ParameterStartupOperationModel.ValidateDuplicate((IParameterStartupMutation)command, previous.CommandName, previous.StreamId, previous.CommandData);
        // A committed Apply can never reactivate a released run. An audited but uncommitted attempt can be retried explicitly.
        return !await (Services.DbEventSource.HasEventForCommandAsync(command.CommandId).WaitAsync(cancellationToken)).ConfigureAwait(false);
    }
    protected override ValueTask OnCommandFinishedAsync(ICommandActorContext<ParameterStartupCommandActor> context, ICommand? command) => command is null ? ValueTask.CompletedTask : ReleaseLease(command);
    protected override ICommand ParseMessage(ICommandActorContext<ParameterStartupCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<ParameterStartupCommandActor> context, ActorThreadId threadId, ICommand command) { ValidateMappedCommand(command, _validationMap); return ValueTask.CompletedTask; }
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<ParameterStartupCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var lease = await (Services.ConfigurationDb.AcquireParameterWriteLeaseAsync()).ConfigureAwait(false);
        if (!leases.TryAdd((command.StreamId, command.CommandId), lease)) { await (lease.DisposeAsync()).ConfigureAwait(false); throw new InvalidOperationException("PARAM.OPERATION_ALREADY_RUNNING"); }
        try { return await (Services.StateRepository.LoadStateAsync(command)).ConfigureAwait(false); }
        catch { await (ReleaseLease(command)).ConfigureAwait(false); throw; }
    }
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<ParameterStartupCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
    { try { await (Services.StateRepository.SaveStateAsync(context, (ParameterStartupCommandState)state, command)).ConfigureAwait(false); } finally { await (ReleaseLease(command)).ConfigureAwait(false); } }
    async ValueTask ReleaseLease(ICommand? cmd) { if (cmd is not null && leases.TryRemove((cmd.StreamId, cmd.CommandId), out var lease)) await (lease.DisposeAsync()).ConfigureAwait(false); }
    protected override async ValueTask OnStartup(ICommandActorContext<ParameterStartupCommandActor> context) => await (Services.EventProjector.StartAsync(context)).ConfigureAwait(false);
    protected override async ValueTask OnShutdown(ICommandActorContext<ParameterStartupCommandActor> context) => await (Services.EventProjector.StopAsync()).ConfigureAwait(false);
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<ParameterStartupCommandActor> context, IActorState state, ICommand command) => await (ResolveMappedCommandHandler(command, _receiveMap)(command, Services, (ParameterStartupCommandState)state)).ConfigureAwait(false);
    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<ParameterStartupCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex) {
        ArgumentNullException.ThrowIfNull(ex); await (ReleaseLease(command)).ConfigureAwait(false); return new ServiceFailed<GuidResult>(command?.ErrorCode ?? 33010, ex.Message); }
}
