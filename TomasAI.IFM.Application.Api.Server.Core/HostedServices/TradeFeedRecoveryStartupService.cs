using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Reuses normal trade loading/restoration after actor and exchange-session startup; never blocks host startup.</summary>
public sealed class TradeFeedRecoveryStartupService(IMarketDataApi market, IFuturesMarketSessionAuthority sessions,
    IActorSupervisor supervisor, IJSActorProducer producer, ILogger<TradeFeedRecoveryStartupService> logger) : BackgroundService
{
    public sealed record Restoration(DateOnly ValueDate, DateTime StartedUtc, bool Restored, string[] TradeIds);
    Restoration? restoration;
    public Restoration? Current => Volatile.Read(ref restoration);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateOnly? attempted = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                var date = sessions.Current.ActiveValueDate;
                if (date is null) { attempted = null; continue; }
                if (attempted == date || !supervisor.IsReady) continue;
                attempted = date;
                Volatile.Write(ref restoration, new(date.Value, DateTime.UtcNow, false, []));
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromMinutes(2));
                Exception? last = null;
                try
                {
                    while (true)
                    {
                        try
                        {
                            if (sessions.Current.ActiveValueDate != date) break;
                            var status = await market.GetBackendStatusAsync(budget.Token).WaitAsync(budget.Token);
                            if (!status.Ready || status.ValueDate != date) throw new InvalidOperationException("Market data session is not ready for trade restoration.");
                            var rows = await market.ReadTradeFeedRecoveryAsync(date.Value, budget.Token).WaitAsync(budget.Token);
                            List<Exception> failures = [];
                            foreach (var row in rows)
                            {
                                try
                                {
                                    var restored = await market.LoadTradeFeedRecoveryAsync(row.TradeId, date.Value, budget.Token).WaitAsync(budget.Token);
                                    if (!restored && (await market.ReadTradeFeedRecoveryAsync(date.Value, budget.Token).WaitAsync(budget.Token)).Any(current => current.TradeId == row.TradeId))
                                        throw new InvalidOperationException("Saved trade feed is still waiting for its active session: " + row.TradeId);
                                }
                                catch (Exception error) when (!budget.IsCancellationRequested)
                                {
                                    failures.Add(error);
                                    logger.LogWarning(error, "Saved trade restoration will retry; TradeId={TradeId}", row.TradeId);
                                }
                            }
                            if (failures.Count != 0) throw new AggregateException("Some saved trade feeds could not be restored.", failures);
                            Volatile.Write(ref restoration, new(date.Value, Current!.StartedUtc, true, rows.Select(row => row.TradeId).ToArray()));
                            logger.LogInformation("Trade feed recovery registrations processed; Count={Count}; ValueDate={ValueDate}", rows.Length, date);
                            break;
                        }
                        catch (Exception error) when (!budget.IsCancellationRequested)
                        { last = error; await Task.Delay(TimeSpan.FromSeconds(1), budget.Token); }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    var failure = new MarketDataDatasetRecoveryFailedEvent
                    {
                        Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = new(date.Value),
                        Subject = new(ActorType.Event, MarketDataDatasetRecoveryFailedEvent.Actor, MarketDataDatasetRecoveryFailedEvent.Verb, new MarketDataFeedId(date.Value).Format()),
                        Dataset = "GLBX.MDP3", Role = "Options", ErrorCode = 503, ErrorType = ErrorType.System,
                        ErrorDate = DateTime.UtcNow, ReceivedOn = DateTime.UtcNow, EventSource = nameof(TradeFeedRecoveryStartupService),
                        ErrorMessage = "Saved trade live feeds could not be restored within two minutes: " + (last ?? error).Message
                    };
                    logger.LogError(error, "{Error}", failure.ErrorMessage);
                    // Retain the startup failure locally until the normal durable UI channel accepts it.
                    while (!stoppingToken.IsCancellationRequested && sessions.Current.ActiveValueDate == date)
                    {
                        try
                        {
                            using var delivery = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); delivery.CancelAfter(TimeSpan.FromSeconds(3));
                            logger.LogWarning("Backend failure notification publishing; Subject={Subject}; OperationId={OperationId}; EventId={EventId}; Detail={Detail}",
                                failure.Subject.ToString(), failure.CommandId, failure.Id, failure.ErrorMessage);
                            if (!producer.IsRunning) await producer.StartAsync(new(ActorType.Event, nameof(TradeFeedRecoveryStartupService)), delivery.Token).AsTask().WaitAsync(delivery.Token);
                            await producer.SendAsync<MarketDataDatasetRecoveryFailedEvent, MarketDataFeedId>(failure.Subject, failure, delivery.Token).AsTask().WaitAsync(delivery.Token);
                            logger.LogError("Backend failure notification published; Subject={Subject}; OperationId={OperationId}; EventId={EventId}; Detail={Detail}",
                                failure.Subject.ToString(), failure.CommandId, failure.Id, failure.ErrorMessage);
                            break;
                        }
                        catch (Exception sendError) when (!stoppingToken.IsCancellationRequested)
                        { logger.LogWarning(sendError, "Startup trade recovery error delivery will retry."); await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
                    }
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            if (producer.IsRunning)
            {
                using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await producer.StopAsync(shutdown.Token).AsTask().WaitAsync(shutdown.Token); }
                catch (Exception error) { logger.LogWarning(error, "Trade recovery notification producer did not stop cleanly."); }
            }
        }
    }
}
