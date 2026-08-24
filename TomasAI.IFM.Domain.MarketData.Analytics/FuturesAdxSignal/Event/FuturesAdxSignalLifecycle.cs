using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;

/// <summary>Provides the FuturesAdxSignalLifecycle implementation.</summary>
public static class FuturesAdxSignalLifecycle
{
    /// <summary>Executes the ExecuteAsync operation.</summary>
    public static ValueTask<bool> ExecuteAsync(this FuturesAdxSignalStartedEvent e, IEventActorContext context,
        IEventActorContext commandApi, IMarketDataApi marketDataApi,
        IStatusConsoleWriter status, ILogger logger)
    {
        try
        {
            e.StartTimer(async entityId =>
            {
                try
                {
                    if (!marketDataApi.IsTickDataStreamActive(entityId.ContractId)
                        || !marketDataApi.TryGetLastTickPrice(entityId.ContractId, out var snapshot)
                        || snapshot.Trade is not { } trade)
                        return;
                    if (!StringComparer.Ordinal.Equals(snapshot.ContractId, entityId.ContractId)
                        || snapshot.ValueDate != entityId.ValueDate
                        || snapshot.AssetTypeId != AssetTypeId.Futures)
                        throw new MarketDataContractMappingException(entityId.ContractId, "the ADX timer entity and hot-cache snapshot identities do not match");
                    if (!e.TryAcceptSourceSequence(trade.SourceSequence)) return;
                    var sourceTimestamp = trade.EventTimestamp.UtcDateTime;
                    await context.SendAsync<FuturesAdxSignalSampledRealtimeEvent, FuturesAdxSignalEntityId>(new()
                    {
                        Subject = new(ActorType.Realtime, FuturesAdxSignalSampledRealtimeEvent.Actor,
                            FuturesAdxSignalSampledRealtimeEvent.Verb, entityId.Format()),
                        Id = Guid.NewGuid(),
                        EntityId = entityId,
                        CommandId = e.CommandId,
                        AggregateId = entityId.Format(),
                        EventSource = nameof(FuturesAdxSignalStartedEvent),
                        ReceivedOn = DateTime.UtcNow,
                        FuturesPrice = trade.LastPrice,
                        SourceSequence = trade.SourceSequence,
                        SourceEventTimestamp = sourceTimestamp
                    });
                }
                catch (Exception ex) { await LogAsync(ex); }
            });
            return ValueTask.FromResult(true);
        }
        catch (Exception ex) { return HandleStartFailureAsync(ex); }

        async ValueTask LogAsync(Exception ex)
        {
            await status.WriteConsoleAsync(LogSourceType.FuturesAdxSignalEvent, FuturesAdxSignalStartedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(nameof(LogSourceType.FuturesAdxSignalEvent), ex.GetErrorMessage(),
                "ADX timer handler failed for {ContractId}", e.EntityId.ContractId);
        }
        async ValueTask<bool> HandleStartFailureAsync(Exception ex) { await LogAsync(ex); return false; }
    }

    /// <summary>Executes the ExecuteAsync operation.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAdxSignalStoppedEvent e, IEventActorContext context,
        IStatusConsoleWriter status, ILogger logger)
    {
        try { await e.StopTimerAsync(); return true; }
        catch (Exception ex)
        {
            await status.WriteConsoleAsync(LogSourceType.FuturesAdxSignalEvent, FuturesAdxSignalStoppedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(nameof(LogSourceType.FuturesAdxSignalEvent), ex.GetErrorMessage(),
                "ADX timer stop failed for {ContractId}", e.EntityId.ContractId);
            return false;
        }
    }
}
