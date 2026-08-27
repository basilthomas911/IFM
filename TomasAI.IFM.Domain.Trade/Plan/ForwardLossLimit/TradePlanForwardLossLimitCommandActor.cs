using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;
using TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.Decorators;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>Provides the TradePlanForwardLossLimitCommandActor implementation.</summary>
public sealed class TradePlanForwardLossLimitCommandActor(
    ICommandActorContext<TradePlanForwardLossLimitCommandActor> actorContext)
    : BaseEventSourceCommandActor<TradePlanForwardLossLimitCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    private ITradePlanForwardLossLimitCommandActorContext ActorContext =>
        IsArgumentNull.Set(Context as ITradePlanForwardLossLimitCommandActorContext, nameof(Context))!;

    public const string ActorName = "TradePlanForwardLossLimitCommand";

    static readonly Dictionary<string, Func<IActorMessage, ICommand>> _parseMap = new()
    {
        [UpdateTradePlanForwardLossLimitCommand.Verb] = message => message.AsCommand<UpdateTradePlanForwardLossLimitCommand>()!,
        [ClearTradePlanForwardLossLimitCommand.Verb] = message => message.AsCommand<ClearTradePlanForwardLossLimitCommand>()!
    };

    static readonly Dictionary<string, Action<ICommand>> _validationMap = new()
    {
        [typeof(UpdateTradePlanForwardLossLimitCommand).Name] = command =>
            new TradePlanForwardLossLimitCommandDecorator().ValidateCommand((UpdateTradePlanForwardLossLimitCommand)command),
        [typeof(ClearTradePlanForwardLossLimitCommand).Name] = command =>
            new TradePlanForwardLossLimitCommandDecorator().ValidateCommand((ClearTradePlanForwardLossLimitCommand)command)
    };

    static readonly Dictionary<string, Func<ICommand, ICommandActorContext<TradePlanForwardLossLimitCommandActor>, TradePlanActorState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap = new()
    {
        [typeof(UpdateTradePlanForwardLossLimitCommand).Name] = (command, context, state) =>
            ((UpdateTradePlanForwardLossLimitCommand)command).ExecuteAsync(context, state),
        [typeof(ClearTradePlanForwardLossLimitCommand).Name] = (command, context, state) =>
            ((ClearTradePlanForwardLossLimitCommand)command).ExecuteAsync(context, state)
    };

    protected override ICommand ParseMessage(ICommandActorContext<TradePlanForwardLossLimitCommandActor> context, IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Command, Name: ActorName }
            || !_parseMap.TryGetValue(message.Subject.Verb, out var parse))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        return parse(message);
    }

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        if (!_validationMap.TryGetValue(command.GetType().Name, out var validate))
            throw new InvalidOperationException($"Unable to validate {ActorName} command: {command.GetType().Name}");
        validate(command);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        IActorState state,
        ICommand command)
    {
        if (!_receiveMap.TryGetValue(command.GetType().Name, out var receive))
            throw new InvalidOperationException($"Unable to process {ActorName} command: {command.GetType().Name}");
        return await receive(command, context, (TradePlanActorState)state).ConfigureAwait(false);
    }

    protected override ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
        => ValueTask.FromResult<IActorState>(new TradePlanActorState { Id = threadId });

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceResult<GuidResult>(command.ErrorCode, ex.Message));
}
