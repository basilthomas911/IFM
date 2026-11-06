using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;
public sealed class FuturesExitPositionWorkflowCommandActor(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> actorContext) : BaseEventSourceCommandActor<FuturesExitPositionWorkflowCommandActor>(actorContext, Typed(actorContext).Logger)
{
    public const string ActorName = StartFuturesExitPositionWorkflowCommand.Actor;
    readonly IFuturesExitPositionWorkflowCommandContext services = Typed(actorContext);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [StartFuturesExitPositionWorkflowCommand.Verb] = static message => message.AsCommand<StartFuturesExitPositionWorkflowCommand>()!,
        [RecordExitOrderCompositionCommand.Verb] = static message => message.AsCommand<RecordExitOrderCompositionCommand>()!,
        [RecordExitRiskDecisionCommand.Verb] = static message => message.AsCommand<RecordExitRiskDecisionCommand>()!,
        [CompleteExitPositionWorkflowCommand.Verb] = static message => message.AsCommand<CompleteExitPositionWorkflowCommand>()!,
        [FailExitPositionWorkflowCommand.Verb] = static message => message.AsCommand<FailExitPositionWorkflowCommand>()!,
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(StartFuturesExitPositionWorkflowCommand)] = static command =>
        {
            var typed = (StartFuturesExitPositionWorkflowCommand)command;
            return new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName).ValidateEntityId(typed.EntityId, typed.CommandName);
        },
        [typeof(RecordExitOrderCompositionCommand)] = static command =>
        {
            var typed = (RecordExitOrderCompositionCommand)command;
            var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName);
            errors.AddRange(ExitWorkflowLifecycleHandlers.Validate(typed, ActorName, RecordExitOrderCompositionCommand.Verb));
            return errors;
        },
        [typeof(RecordExitRiskDecisionCommand)] = static command =>
        {
            var typed = (RecordExitRiskDecisionCommand)command;
            var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName);
            errors.AddRange(ExitWorkflowLifecycleHandlers.Validate(typed, ActorName, RecordExitRiskDecisionCommand.Verb));
            return errors;
        },
        [typeof(CompleteExitPositionWorkflowCommand)] = static command =>
        {
            var typed = (CompleteExitPositionWorkflowCommand)command;
            var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName);
            errors.AddRange(ExitWorkflowLifecycleHandlers.Validate(typed, ActorName, CompleteExitPositionWorkflowCommand.Verb));
            return errors;
        },
        [typeof(FailExitPositionWorkflowCommand)] = static command =>
        {
            var typed = (FailExitPositionWorkflowCommand)command;
            var errors = new List<ValidationError>().ValidateCommandId(typed.CommandId, typed.CommandName);
            errors.AddRange(ExitWorkflowLifecycleHandlers.Validate(typed, ActorName, FailExitPositionWorkflowCommand.Verb));
            return errors;
        },
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ICommand, FuturesExitPositionWorkflowCommandState, ServiceResult<GuidResult>>> _receiveMap = new Dictionary<Type, Func<ICommand, FuturesExitPositionWorkflowCommandState, ServiceResult<GuidResult>>>
    {
        [typeof(StartFuturesExitPositionWorkflowCommand)] = static (command, state) => ((StartFuturesExitPositionWorkflowCommand)command).Execute(state),
        [typeof(RecordExitOrderCompositionCommand)] = static (command, state) => ((RecordExitOrderCompositionCommand)command).Execute(state),
        [typeof(RecordExitRiskDecisionCommand)] = static (command, state) => ((RecordExitRiskDecisionCommand)command).Execute(state),
        [typeof(CompleteExitPositionWorkflowCommand)] = static (command, state) => ((CompleteExitPositionWorkflowCommand)command).Execute(state),
        [typeof(FailExitPositionWorkflowCommand)] = static (command, state) => ((FailExitPositionWorkflowCommand)command).Execute(state),
    }.ToFrozenDictionary();
    protected override bool AuditIsCommittedWithState(ICommand command) => string.Equals(services.DbFactory.TradePlanDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal);
    protected override bool IsCommittedDuplicateException(ICommand command, Exception exception) =>
        AuditIsCommittedWithState(command) && exception is CommandAuditDuplicateException { HasCommittedEvents: true } duplicate &&
        duplicate.CommandId == command.CommandId;
    protected override ValueTask OnStartup(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context) => services.EventProjector.StartAsync(context);
    protected override ValueTask OnShutdown(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context) => services.EventProjector.StopAsync();
    protected override ICommand ParseMessage(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId, ICommand command) => await services.StateRepository.LoadStateAsync(command).ConfigureAwait(false);
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command) => await services.StateRepository.SaveStateAsync(context, (FuturesExitPositionWorkflowCommandState)state, command).ConfigureAwait(false);
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId, ICommand command, CancellationToken token) => await services.StateRepository.LoadStateAsync(command, token).ConfigureAwait(false);
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command, CancellationToken token) => services.StateRepository.SaveStateAsync(context, (FuturesExitPositionWorkflowCommandState)state, command, token);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, IActorState state, ICommand command) => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (FuturesExitPositionWorkflowCommandState)state));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ex);
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, ex.Message));
    }

    static IFuturesExitPositionWorkflowCommandContext Typed(ICommandActorContext<FuturesExitPositionWorkflowCommandActor> context) => context as IFuturesExitPositionWorkflowCommandContext ?? throw new ArgumentException("Typed Futures exit-workflow context required.", nameof(context));
}
