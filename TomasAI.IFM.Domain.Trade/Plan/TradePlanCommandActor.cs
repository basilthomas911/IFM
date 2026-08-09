using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Plan;

public sealed class TradePlanCommandActor(
    IDbContextFactory dbFactory,
    ITradeEventProducer eventProducer,
    ILogger<TradePlanCommandActor> logger)
    : BaseEventSourceCommandActor<TradePlanCommandActor>(
        logger,
        new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "TradePlanCommand";

    protected override ICommand ParseMessage(ICommandActorContext context, IActorMessage message)
    {
        if (!message.Subject.Is(ActorType.Command, ActorName, UpdateTradePlanCommand.Verb))
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");
        return message.AsCommand<UpdateTradePlanCommand>()!;
    }

    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext context,
        IActorState state,
        ICommand command)
    {
        var update = (UpdateTradePlanCommand)command;
        await dbFactory.TradeDb.InsertTradePlanAsync(update.TradePlan);
        await eventProducer.PostEventAsync(new TradePlanUpdatedEvent
        {
            CommandId = update.CommandId,
            EntityId = update.EntityId,
            TradePlan = update.TradePlan
        });
        return new ServiceOk<GuidResult>(new GuidResult(update.CommandId));
    }

    protected override ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext context,
        ActorThreadId threadId,
        ICommand command)
        => ValueTask.FromResult<IActorState>(new TradePlanActorState { Id = threadId });

    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext context,
        ActorThreadId threadId,
        ICommand command,
        Exception ex)
        => ValueTask.FromResult<ServiceResult<GuidResult>>(
            new ServiceResult<GuidResult>(command.ErrorCode, ex.Message));
}

sealed class TradePlanActorState : IActorState<TradePlanActorState>
{
    public ActorThreadId Id { get; set; } = default!;
}
