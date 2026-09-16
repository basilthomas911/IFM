using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Validation;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;

/// <summary>Serializes the durable execution lifecycle for one accepted TradeOrder attempt.</summary>
public sealed class OrderExecutionCommandActor(ICommandActorContext<OrderExecutionCommandActor> context)
    : BaseEventSourceCommandActor<OrderExecutionCommandActor>(context, Typed(context).Logger)
{
    public const string ActorName = OrderExecutionActorNames.Command;

    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
        {
            [StartOrderExecutionCommand.Verb] = message => message.AsCommand<StartOrderExecutionCommand>()!,
            [SubmitOrderExecutionCommand.Verb] = message => message.AsCommand<SubmitOrderExecutionCommand>()!,
            [AddOrderExecutionFillCommand.Verb] = message => message.AsCommand<AddOrderExecutionFillCommand>()!,
            [UpdateOrderExecutionFillCostCommand.Verb] = message => message.AsCommand<UpdateOrderExecutionFillCostCommand>()!,
            [AcceptOrderExecutionCommand.Verb] = message => message.AsCommand<AcceptOrderExecutionCommand>()!,
            [CancelOrderExecutionCommand.Verb] = message => message.AsCommand<CancelOrderExecutionCommand>()!,
            [RejectOrderExecutionCommand.Verb] = message => message.AsCommand<RejectOrderExecutionCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(StartOrderExecutionCommand)] = OrderExecutionCommandValidation.Validate,
            [typeof(SubmitOrderExecutionCommand)] = OrderExecutionCommandValidation.Validate,
            [typeof(AddOrderExecutionFillCommand)] = OrderExecutionCommandValidation.Validate,
            [typeof(UpdateOrderExecutionFillCostCommand)] = OrderExecutionCommandValidation.Validate,
            [typeof(AcceptOrderExecutionCommand)] = OrderExecutionCommandValidation.Validate,
            [typeof(CancelOrderExecutionCommand)] = OrderExecutionCommandValidation.Validate,
            [typeof(RejectOrderExecutionCommand)] = OrderExecutionCommandValidation.Validate
        }.ToFrozenDictionary();

    private static readonly IReadOnlyDictionary<Type,
        Func<ICommand, OrderExecutionCommandState, ServiceResult<GuidResult>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, OrderExecutionCommandState, ServiceResult<GuidResult>>>
        {
            [typeof(StartOrderExecutionCommand)] = static (command, state) => ((StartOrderExecutionCommand)command).Execute(state),
            [typeof(SubmitOrderExecutionCommand)] = static (command, state) => ((SubmitOrderExecutionCommand)command).Execute(state),
            [typeof(AddOrderExecutionFillCommand)] = static (command, state) => ((AddOrderExecutionFillCommand)command).Execute(state),
            [typeof(UpdateOrderExecutionFillCostCommand)] = static (command, state) => ((UpdateOrderExecutionFillCostCommand)command).Execute(state),
            [typeof(AcceptOrderExecutionCommand)] = static (command, state) => ((AcceptOrderExecutionCommand)command).Execute(state),
            [typeof(CancelOrderExecutionCommand)] = static (command, state) => ((CancelOrderExecutionCommand)command).Execute(state),
            [typeof(RejectOrderExecutionCommand)] = static (command, state) => ((RejectOrderExecutionCommand)command).Execute(state)
        }.ToFrozenDictionary();

    private readonly IOrderExecutionCommandContext _services = Typed(context);

    /// <inheritdoc />
    protected override ValueTask OnStartup(ICommandActorContext<OrderExecutionCommandActor> actorContext) =>
        _services.EventProjector.StartAsync(actorContext);

    /// <inheritdoc />
    protected override ValueTask OnShutdown(ICommandActorContext<OrderExecutionCommandActor> actorContext) =>
        _services.EventProjector.StopAsync();

    /// <inheritdoc />
    protected override ICommand ParseMessage(ICommandActorContext<OrderExecutionCommandActor> actorContext,
        IActorMessage message) => ParseMappedCommand(actorContext, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask OnValidateAsync(ICommandActorContext<OrderExecutionCommandActor> actorContext,
        ActorThreadId actorThreadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<OrderExecutionCommandActor> actorContext,
        ActorThreadId actorThreadId, ICommand command) =>
        await _services.StateRepository.LoadStateAsync(command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext<OrderExecutionCommandActor> actorContext,
        ActorThreadId actorThreadId, IActorState state, ICommand command) =>
        await _services.StateRepository.SaveStateAsync(actorContext,
            (OrderExecutionCommandState)state, command).ConfigureAwait(false);

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<OrderExecutionCommandActor> actorContext,
        IActorState state, ICommand command) => ValueTask.FromResult(
        ResolveMappedCommandHandler(command, _receiveMap)(command, (OrderExecutionCommandState)state));

    /// <inheritdoc />
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<OrderExecutionCommandActor> actorContext,
        ActorThreadId actorThreadId, ICommand command, Exception exception) =>
        ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceFailed<GuidResult>(command.ErrorCode, exception.Message));

    private static IOrderExecutionCommandContext Typed(ICommandActorContext<OrderExecutionCommandActor> context) =>
        context as IOrderExecutionCommandContext ??
        throw new ArgumentException("Typed Order Execution context required.");
}
