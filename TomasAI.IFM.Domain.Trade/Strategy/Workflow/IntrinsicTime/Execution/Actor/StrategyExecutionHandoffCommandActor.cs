using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Actor;

/// <summary>Serializes opening-intent acceptance and prepared execution evidence under one durable stream.</summary>
public sealed class StrategyExecutionHandoffCommandActor(ICommandActorContext<StrategyExecutionHandoffCommandActor> context)
    : BaseEventSourceCommandActor<StrategyExecutionHandoffCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = BeginStrategyExecutionHandoffCommand.Actor;
    readonly IStrategyExecutionHandoffCommandContext services = Typed(context);
    readonly Lazy<IEventProjector<StrategyExecutionHandoffCommandActor>> projector =
        new(() => Typed(context).EventProjector);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [BeginStrategyExecutionHandoffCommand.Verb] = static message => message.AsCommand<BeginStrategyExecutionHandoffCommand>()!,
        [AdvanceStrategyExecutionHandoffCommand.Verb] = static message => message.AsCommand<AdvanceStrategyExecutionHandoffCommand>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(BeginStrategyExecutionHandoffCommand)] = static command => new List<ValidationError>()
                .ValidateCommandId(command.CommandId, command.CommandName).ValidateHandoffIdentity(command),
            [typeof(AdvanceStrategyExecutionHandoffCommand)] = static command => new List<ValidationError>()
                .ValidateCommandId(command.CommandId, command.CommandName).ValidateHandoffIdentity(command)
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ICommand, StrategyExecutionHandoffCommandState, DateTime, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, StrategyExecutionHandoffCommandState, DateTime, ServiceResult<GuidResult>>>
        {
            [typeof(BeginStrategyExecutionHandoffCommand)] = static (command, state, now) => ((BeginStrategyExecutionHandoffCommand)command).Execute(state, now),
            [typeof(AdvanceStrategyExecutionHandoffCommand)] = static (command, state, now) => ((AdvanceStrategyExecutionHandoffCommand)command).Execute(state, now)
        }.ToFrozenDictionary();

    protected override ValueTask OnStartup(ICommandActorContext<StrategyExecutionHandoffCommandActor> context)
        => projector.Value.StartAsync(context);

    protected override ValueTask OnShutdown(ICommandActorContext<StrategyExecutionHandoffCommandActor> context)
        => projector.IsValueCreated ? projector.Value.StopAsync() : ValueTask.CompletedTask;
    protected override ICommand ParseMessage(ICommandActorContext<StrategyExecutionHandoffCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override async ValueTask OnValidateAsync(ICommandActorContext<StrategyExecutionHandoffCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        if (command is BeginStrategyExecutionHandoffCommand begin &&
            !await context.Container.Resolve<StrategyExecutionHandoffReadStore>().VerifyAuthorizationAsync(begin.Intent).ConfigureAwait(false))
            throw new InvalidOperationException("EXECUTION.AUTHORIZATION.SOURCE_MISMATCH");
        if (command is AdvanceStrategyExecutionHandoffCommand advance && advance.Evidence.Phase >= StrategyExecutionHandoffPhase.Opened &&
            !await context.Container.Resolve<StrategyOpeningEvidenceVerifier>().VerifyAsync(advance.Evidence).ConfigureAwait(false))
            throw new InvalidOperationException("EXECUTION.TERMINAL_EVIDENCE.SOURCE_MISMATCH");
    }
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<StrategyExecutionHandoffCommandActor> context, ActorThreadId threadId, ICommand command)
        => await services.StateRepository.LoadStateAsync(command).ConfigureAwait(false);
    protected override ValueTask OnSaveStateAsync(ICommandActorContext<StrategyExecutionHandoffCommandActor> context, ActorThreadId threadId, IActorState state, ICommand command)
        => services.StateRepository.SaveStateAsync(context, (StrategyExecutionHandoffCommandState)state, command);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<StrategyExecutionHandoffCommandActor> context, IActorState state, ICommand command)
        => ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (StrategyExecutionHandoffCommandState)state, services.TimeProvider.GetUtcNow().UtcDateTime));
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<StrategyExecutionHandoffCommandActor> context, ActorThreadId threadId, ICommand command, Exception error)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(command.ErrorCode, error.Message));
    static IStrategyExecutionHandoffCommandContext Typed(ICommandActorContext<StrategyExecutionHandoffCommandActor> context)
        => context as IStrategyExecutionHandoffCommandContext ?? throw new ArgumentException("Typed opening execution context required.", nameof(context));

}
