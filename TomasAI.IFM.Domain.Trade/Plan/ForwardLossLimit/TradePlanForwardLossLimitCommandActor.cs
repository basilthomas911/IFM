using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

public sealed class TradePlanForwardLossLimitCommandActor(
    IDbContextFactory dbFactory,
    ITradeEventProducer eventProducer,
    ILogger<TradePlanForwardLossLimitCommandActor> logger)
    : BaseEventSourceCommandActor<TradePlanForwardLossLimitCommandActor>(
        logger,
        new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "TradePlanForwardLossLimitCommand";

    protected override ICommand ParseMessage(ICommandActorContext context, IActorMessage message)
        => message.Subject switch
        {
            { ActorType: ActorType.Command, Name: ActorName, Verb: UpdateTradePlanForwardLossLimitCommand.Verb }
                => message.AsCommand<UpdateTradePlanForwardLossLimitCommand>()!,
            { ActorType: ActorType.Command, Name: ActorName, Verb: ClearTradePlanForwardLossLimitCommand.Verb }
                => message.AsCommand<ClearTradePlanForwardLossLimitCommand>()!,
            _ => throw new InvalidOperationException(
                $"Unable to resolve {ActorName} command from message: {message.Subject}")
        };

    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext context,
        IActorState state,
        ICommand command)
    {
        switch (command)
        {
            case UpdateTradePlanForwardLossLimitCommand update:
                await dbFactory.TradeDb.InsertTradePlanForwardLossLimitAsync(update.TradePlanForwardLossLimit);
                await eventProducer.PostEventAsync(new TradePlanForwardLossLimitUpdatedEvent
                {
                    CommandId = update.CommandId,
                    EntityId = update.EntityId.Format(),
                    TradePlanForwardLossLimit = update.TradePlanForwardLossLimit,
                    UpdatedOn = update.OriginatedOn,
                    UpdatedBy = update.OriginatedBy
                });
                break;
            case ClearTradePlanForwardLossLimitCommand clear:
                await dbFactory.TradeDb.DeleteTradePlanForwardLossLimitAsync(clear.EntityId);
                await eventProducer.PostEventAsync(new TradePlanForwardLossLimitClearedEvent
                {
                    CommandId = clear.CommandId,
                    EntityId = clear.EntityId.Format(),
                    ForwardLossLimitId = clear.EntityId,
                    ClearedOn = clear.OriginatedOn,
                    ClearedBy = clear.OriginatedBy
                });
                break;
            default:
                throw new InvalidOperationException(
                    $"Unable to process {ActorName} command: {command.GetType().Name}");
        }

        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
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
