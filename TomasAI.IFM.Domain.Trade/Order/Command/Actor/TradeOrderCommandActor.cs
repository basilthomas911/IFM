using TomasAI.IFM.Domain.Trade.Shared;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Order.Command;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Command.Validation;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Actor;

/// <summary>Serializes the event-sourced Trade Order lifecycle using exact-type command dispatch.</summary>
public sealed class TradeOrderCommandActor(ICommandActorContext<TradeOrderCommandActor> context)
    : BaseEventSourceCommandActor<TradeOrderCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = TradeOrderActorNames.Command;
    private readonly ITradeOrderCommandContext _services = Typed(context);
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [CreateTradeOrderCommand.Verb] = static m => m.AsCommand<CreateTradeOrderCommand>()!,
            [AmendTradeOrderCommand.Verb] = static m => m.AsCommand<AmendTradeOrderCommand>()!,
            [ApproveTradeOrderCommand.Verb] = static m => m.AsCommand<ApproveTradeOrderCommand>()!,
            [ReadyTradeOrderCommand.Verb] = static m => m.AsCommand<ReadyTradeOrderCommand>()!,
            [BindTradeOrderExecutionCommand.Verb] = static m => m.AsCommand<BindTradeOrderExecutionCommand>()!,
            [ReleaseTradeOrderExecutionCommand.Verb] = static m => m.AsCommand<ReleaseTradeOrderExecutionCommand>()!,
            [CompleteTradeOrderCommand.Verb] = static m => m.AsCommand<CompleteTradeOrderCommand>()!,
            [CancelTradeOrderCommand.Verb] = static m => m.AsCommand<CancelTradeOrderCommand>()!,
            [ExpireTradeOrderCommand.Verb] = static m => m.AsCommand<ExpireTradeOrderCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CreateTradeOrderCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(AmendTradeOrderCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(ApproveTradeOrderCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(ReadyTradeOrderCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(BindTradeOrderExecutionCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(ReleaseTradeOrderExecutionCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(CompleteTradeOrderCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(CancelTradeOrderCommand)] = TradeOrderCommandValidation.Validate,
            [typeof(ExpireTradeOrderCommand)] = TradeOrderCommandValidation.Validate
        }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<ICommand, TradeOrderCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, TradeOrderCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(CreateTradeOrderCommand)] = static (c, s) => ((CreateTradeOrderCommand)c).Execute(s),
            [typeof(AmendTradeOrderCommand)] = static (c, s) => ((AmendTradeOrderCommand)c).Execute(s),
            [typeof(ApproveTradeOrderCommand)] = static (c, s) => ((ApproveTradeOrderCommand)c).Execute(s),
            [typeof(ReadyTradeOrderCommand)] = static (c, s) => ((ReadyTradeOrderCommand)c).Execute(s),
            [typeof(BindTradeOrderExecutionCommand)] = static (c, s) => ((BindTradeOrderExecutionCommand)c).Execute(s),
            [typeof(ReleaseTradeOrderExecutionCommand)] = static (c, s) => ((ReleaseTradeOrderExecutionCommand)c).Execute(s),
            [typeof(CompleteTradeOrderCommand)] = static (c, s) => ((CompleteTradeOrderCommand)c).Execute(s),
            [typeof(CancelTradeOrderCommand)] = static (c, s) => ((CancelTradeOrderCommand)c).Execute(s),
            [typeof(ExpireTradeOrderCommand)] = static (c, s) => ((ExpireTradeOrderCommand)c).Execute(s)
        }.ToFrozenDictionary();

    /// <inheritdoc />
    protected override ValueTask OnStartup(ICommandActorContext<TradeOrderCommandActor> context) =>
        _services.EventProjector.StartAsync(context);

    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<TradeOrderCommandActor> context) =>
        _services.EventProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<TradeOrderCommandActor> context,
        IActorMessage message) => ParseMappedCommand(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<TradeOrderCommandActor> context,
        ActorThreadId actorThreadId, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<TradeOrderCommandActor> context, ActorThreadId actorThreadId, ICommand command) =>
        await _services.StateRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext<TradeOrderCommandActor> context,
        ActorThreadId actorThreadId, IActorState state, ICommand command) =>
        await _services.StateRepository.SaveStateAsync(context, (TradeOrderCommandState)state, command)
            .ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<TradeOrderCommandActor> context, IActorState state, ICommand command) =>
        ValueTask.FromResult(ResolveMappedCommandHandler(command, _receiveMap)(command, (TradeOrderCommandState)state));

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<TradeOrderCommandActor> context, ActorThreadId actorThreadId, ICommand command,
        Exception exception) => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));

    static ITradeOrderCommandContext Typed(ICommandActorContext<TradeOrderCommandActor> c) => c as ITradeOrderCommandContext ?? throw new ArgumentException("Typed Trade Order command context required.");
}
