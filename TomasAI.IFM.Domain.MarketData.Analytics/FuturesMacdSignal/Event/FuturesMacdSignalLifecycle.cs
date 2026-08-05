using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

public static class FuturesMacdSignalLifecycle
{
    public static ValueTask<bool> ExecuteAsync(this FuturesMacdSignalStartedEvent e, IEventActorContext context,
        IActorMarketDataAnalyticsCommandApi commandApi, IStatusConsoleWriter status, ILogger logger)
    {
        try
        {
            e.StartTimer(async entityId =>
            {
                try
                {
                    if (!entityId.ContractId.StartsWith("ES", StringComparison.Ordinal))
                        return;
                    var data = await context.GetLastFuturesEodDataAsync(entityId.ContractId, entityId.ValueDate);
                    if (data is null)
                        return;
                    var id = new FuturesMacdSignalId(entityId.ContractId, entityId.ValueDate, entityId.TimePeriod,
                        entityId.PeriodLength, TimeOnly.FromDateTime(DateTime.UtcNow));
                    _ = await commandApi.GenerateFuturesMacdSignalAsync(id, data.ClosePrice);
                }
                catch (Exception ex)
                {
                    await LogAsync(ex);
                }
            });
            return ValueTask.FromResult(true);
        }
        catch (Exception ex)
        {
            return HandleStartFailureAsync(ex);
        }

        async ValueTask LogAsync(Exception ex)
        {
            await status.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent, FuturesMacdSignalStartedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(nameof(LogSourceType.FuturesMacdSignalEvent), ex.GetErrorMessage(),
                "MACD timer handler failed for {ContractId}", e.EntityId.ContractId);
        }
        async ValueTask<bool> HandleStartFailureAsync(Exception ex) { await LogAsync(ex); return false; }
    }

    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdSignalStoppedEvent e, IEventActorContext context,
        IStatusConsoleWriter status, ILogger logger)
    {
        try { await e.StopTimerAsync(); return true; }
        catch (Exception ex)
        {
            await status.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent, FuturesMacdSignalStoppedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(nameof(LogSourceType.FuturesMacdSignalEvent), ex.GetErrorMessage(),
                "MACD timer stop failed for {ContractId}", e.EntityId.ContractId);
            return false;
        }
    }
}
