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
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

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

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [UpdateTradePlanForwardLossLimitCommand.Verb] = message => message.AsCommand<UpdateTradePlanForwardLossLimitCommand>()!,
        [ClearTradePlanForwardLossLimitCommand.Verb] = message => message.AsCommand<ClearTradePlanForwardLossLimitCommand>()!
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(UpdateTradePlanForwardLossLimitCommand)] = command =>
        {
            var update = (UpdateTradePlanForwardLossLimitCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(update.CommandId, update.CommandName)
                .ValidateEntityId(update.EntityId, update.CommandName)
                .CaptureCommandValidation(() =>
                    new TradePlanForwardLossLimitCommandDecorator().ValidateCommand(update));
        },
        [typeof(ClearTradePlanForwardLossLimitCommand)] = command =>
        {
            var clear = (ClearTradePlanForwardLossLimitCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(clear.CommandId, clear.CommandName)
                .ValidateEntityId(clear.EntityId, clear.CommandName)
                .CaptureCommandValidation(() =>
                    new TradePlanForwardLossLimitCommandDecorator().ValidateCommand(clear));
        }
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<TradePlanForwardLossLimitCommandActor>, TradePlanActorState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<TradePlanForwardLossLimitCommandActor>, TradePlanActorState, ValueTask<ServiceResult<GuidResult>>>>()
    {
        [typeof(UpdateTradePlanForwardLossLimitCommand)] = (command, context, state) =>
            ((UpdateTradePlanForwardLossLimitCommand)command).ExecuteAsync(context, state),
        [typeof(ClearTradePlanForwardLossLimitCommand)] = (command, context, state) =>
            ((ClearTradePlanForwardLossLimitCommand)command).ExecuteAsync(context, state)
    };

    protected override ICommand ParseMessage(
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        IActorState state,
        ICommand command)
    {
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
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
