using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Domain.Trade.Plan.Decorators;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Provides the TradePlanCommandActor implementation.</summary>
public sealed class TradePlanCommandActor(
    ICommandActorContext<TradePlanCommandActor> actorContext)
    : BaseEventSourceCommandActor<TradePlanCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    private ITradePlanCommandActorContext ActorContext =>
        IsArgumentNull.Set(Context as ITradePlanCommandActorContext, nameof(Context))!;

    public const string ActorName = "TradePlanCommand";

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>()
    {
        [UpdateTradePlanCommand.Verb] = message => message.AsCommand<UpdateTradePlanCommand>()!
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(UpdateTradePlanCommand)] = command =>
        {
            var update = (UpdateTradePlanCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(update.CommandId, update.CommandName)
                .ValidateEntityId(update.EntityId, update.CommandName)
                .CaptureCommandValidation(() =>
                    new TradePlanCommandDecorator().ValidateCommand(update));
        }
    };

    static readonly IReadOnlyDictionary<Type, Func<ICommand, ICommandActorContext<TradePlanCommandActor>, TradePlanActorState, ValueTask<ServiceResult<GuidResult>>>> _receiveMap = new Dictionary<Type, Func<ICommand, ICommandActorContext<TradePlanCommandActor>, TradePlanActorState, ValueTask<ServiceResult<GuidResult>>>>()
    {
        [typeof(UpdateTradePlanCommand)] = (command, context, state) =>
            ((UpdateTradePlanCommand)command).ExecuteAsync(context, state)
    };

    protected override ICommand ParseMessage(
        ICommandActorContext<TradePlanCommandActor> context,
        IActorMessage message)
        => ParseMappedCommand(context, message, _parseMap);

    protected override ValueTask OnValidateAsync(
        ICommandActorContext<TradePlanCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<TradePlanCommandActor> context,
        IActorState state,
        ICommand command)
    {
        var receive = ResolveMappedCommandHandler(command, _receiveMap);
        return await receive(command, context, (TradePlanActorState)state).ConfigureAwait(false);
    }

    protected override ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<TradePlanCommandActor> context,
        ActorThreadId threadId,
        ICommand command)
        => ValueTask.FromResult<IActorState>(new TradePlanActorState { Id = threadId });

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext<TradePlanCommandActor> context,
        ActorThreadId threadId,
        ICommand command,
        Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceResult<GuidResult>(command.ErrorCode, ex.Message));
}

sealed class TradePlanActorState : IActorState<TradePlanActorState>
{
    /// <summary>Gets the Id value.</summary>
    public ActorThreadId Id { get; set; } = default!;
}
