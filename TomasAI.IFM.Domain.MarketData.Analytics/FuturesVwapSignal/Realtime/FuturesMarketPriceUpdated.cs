using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime;

/// <summary>Forwards a complete tick-source VWAP checkpoint to the existing projection path.</summary>
public static class FuturesMarketPriceUpdated
{
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IFuturesVwapSignalRealtimeContext context,
        FuturesContractV3ReadModel currentContract,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentContract);
        ArgumentNullException.ThrowIfNull(logger);
        var checkpoint = @event.VwapCheckpoint;
        if (checkpoint is null || !checkpoint.IsReplayComplete
            || !StringComparer.Ordinal.Equals(@event.EntityId.ContractId, currentContract.ContractId))
            return true;
        var configuration = FuturesVwapConfiguration.Standard;
        var entityId = new FuturesVwapSignalEntityId(
            @event.EntityId.ContractId, @event.EntityId.ValueDate, configuration.ConfigurationId);
        var session = context.SessionCalendar.GetSession(@event.EntityId.ValueDate);
        var result = await context.UpdateFuturesVwapSignalAsync(entityId,
            checkpoint, session.StartUtc, session.EndUtc, configuration).ConfigureAwait(false);
        if (result is ServiceFailed<GuidResult> failed)
        {
            logger.LogError("VWAP checkpoint rejected for {ContractId} ordinal {TradeOrdinal}: {Error}",
                entityId.ContractId, checkpoint.LastTradeOrdinal, failed.ErrorMessage);
            return false;
        }
        return true;
    }
}
