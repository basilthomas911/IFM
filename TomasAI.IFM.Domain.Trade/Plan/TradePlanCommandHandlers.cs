using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Provides command handlers for trade-plan persistence and publication.</summary>
internal static class TradePlanCommandHandlers
{
    /// <summary>Persists a trade plan and publishes its updated event.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this UpdateTradePlanCommand command,
        ICommandActorContext<TradePlanCommandActor> context,
        TradePlanActorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var domainContext = IsArgumentNull.Set(context as ITradePlanCommandActorContext, nameof(context))!;
        await domainContext.DbFactory.TradeDb.InsertTradePlanAsync(command.TradePlan).ConfigureAwait(false);
        await domainContext.EventProducer.PostEventAsync(new TradePlanUpdatedEvent
        {
            CommandId = command.CommandId,
            EntityId = command.EntityId,
            TradePlan = command.TradePlan
        }).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }
}
