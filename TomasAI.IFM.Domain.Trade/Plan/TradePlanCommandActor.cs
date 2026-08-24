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

using TomasAI.IFM.Domain.Trade.Plan;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Provides the TradePlanCommandActor implementation.</summary>
public sealed class TradePlanCommandActor(
    ICommandActorContext<TradePlanCommandActor> actorContext)
    : BaseEventSourceCommandActor<TradePlanCommandActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    private ITradePlanCommandActorContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as ITradePlanCommandActorContext, nameof(actorContext))!;

    public const string ActorName = "TradePlanCommand";

    protected override ICommand ParseMessage(ICommandActorContext<TradePlanCommandActor> context, IActorMessage message)
    {
        if (!message.Subject.Is(ActorType.Command, ActorName, UpdateTradePlanCommand.Verb))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        return message.AsCommand<UpdateTradePlanCommand>()!;
    }

    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<TradePlanCommandActor> context,
        IActorState state,
        ICommand command)
    {
        var update = (UpdateTradePlanCommand)command;
        await actorContext.DbFactory.TradeDb.InsertTradePlanAsync(update.TradePlan);
        await actorContext.EventProducer.PostEventAsync(new TradePlanUpdatedEvent
        {
            CommandId = update.CommandId,
            EntityId = update.EntityId,
            TradePlan = update.TradePlan
        });
        return new ServiceOk<GuidResult>(new GuidResult(update.CommandId));
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
