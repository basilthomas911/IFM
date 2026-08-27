using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>Provides command handlers for trade-plan forward-loss-limit persistence and publication.</summary>
internal static class TradePlanForwardLossLimitCommandHandlers
{
    /// <summary>Persists a forward loss limit and publishes its updated event.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this UpdateTradePlanForwardLossLimitCommand command,
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        TradePlanActorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var domainContext = IsArgumentNull.Set(context as ITradePlanForwardLossLimitCommandActorContext, nameof(context))!;
        await domainContext.DbFactory.TradeDb.InsertTradePlanForwardLossLimitAsync(command.TradePlanForwardLossLimit).ConfigureAwait(false);
        await domainContext.EventProducer.PostEventAsync(new TradePlanForwardLossLimitUpdatedEvent
        {
            CommandId = command.CommandId,
            EntityId = command.EntityId.Format(),
            TradePlanForwardLossLimit = command.TradePlanForwardLossLimit,
            UpdatedOn = command.OriginatedOn,
            UpdatedBy = command.OriginatedBy
        }).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }

    /// <summary>Deletes a forward loss limit and publishes its cleared event.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this ClearTradePlanForwardLossLimitCommand command,
        ICommandActorContext<TradePlanForwardLossLimitCommandActor> context,
        TradePlanActorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var domainContext = IsArgumentNull.Set(context as ITradePlanForwardLossLimitCommandActorContext, nameof(context))!;
        await domainContext.DbFactory.TradeDb.DeleteTradePlanForwardLossLimitAsync(command.EntityId).ConfigureAwait(false);
        await domainContext.EventProducer.PostEventAsync(new TradePlanForwardLossLimitClearedEvent
        {
            CommandId = command.CommandId,
            EntityId = command.EntityId.Format(),
            ForwardLossLimitId = command.EntityId,
            ClearedOn = command.OriginatedOn,
            ClearedBy = command.OriginatedBy
        }).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }
}
