using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Publishes data-quality expiry transitions even when the market is silent.</summary>
public sealed class TradeRealtimeCacheExpiryService(TradeRealtimeCacheRegistry cache, IActorProducer producer,
    ILogger<TradeRealtimeCacheExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                foreach (var snapshot in cache.Expire())
                {
                    try
                    {
                        var changed = TradeRealtimeSnapshotChangedEvent.From(snapshot);
                        await producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(changed.Subject, changed, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception error) { logger.LogError(error, "Trade cache expiry notification failed for {TradeId}", snapshot.TradeId.Format()); }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
