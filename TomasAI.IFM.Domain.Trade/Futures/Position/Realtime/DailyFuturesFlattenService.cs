using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;

/// <summary>Restartable clock observations for Daily ATR positions, independently of market ticks and ITI entry gates.</summary>
public sealed class DailyFuturesFlattenService(IDbContextFactory database, IActorSupervisor supervisor,
    TimeProvider clock, ILogger<DailyFuturesFlattenService> logger) : BackgroundService
{
    public static FuturesPositionExitClockEvent? Create(StrategyPositionSnapshot position, DateTime now)
    {
        if (!position.IsOpen || position.StrategyKind != TradeStrategyKind.FuturesOutright
            || position.DailyFuturesEntry is not { } entry || now < DailyFuturesSession.FlattenUtc(entry.ValueDate)) return null;
        var id = TradePlanContractIdentity.DeterministicId($"{position.Id.Format()}|{position.PositionSequence}|{entry.DcTriggerId:N}|flatten-clock");
        return new() { Id = id, CommandId = id, EntityId = position.Id, AggregateId = position.Id.Format(),
            Subject = new(ActorType.Realtime, FuturesTradePositionRealtimeActor.ActorName, FuturesPositionExitClockEvent.Verb, position.Id.Format()),
            PositionSnapshot = position, ReceivedOn = now, EventSource = nameof(DailyFuturesFlattenService) };
    }

    public async Task<bool> RunOnceAsync(CancellationToken token)
    {
        if (!supervisor.IsReady) return false;
        var pending = false;
        var store = (IOpenPositionEndOfDayStore)database.TradeDb;
        StrategyPositionId? after = null;
        do
        {
            var page = await store.ReadOpenAsync(after,64,token).ConfigureAwait(false);
            foreach (var position in page)
                if (Create(position, clock.GetUtcNow().UtcDateTime) is { } notification)
                {
                    pending = true;
                    await supervisor.GetProducer(notification.Subject.ActorId)
                        .SendAsync<FuturesPositionExitClockEvent,StrategyPositionId>(notification.Subject,notification,token).ConfigureAwait(false);
                }
            if (page.Length < 64) break;
            after = page[^1].Id;
        } while (!token.IsCancellationRequested);
        return !pending;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var recovered = false;
        DateOnly? completed = null;
        do
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("America/New_York")));
            if (recovered && (now < DailyFuturesSession.FlattenUtc(today) || completed == today)) continue;
            try
            {
                recovered = await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                // Continue through the late-fill/reconciliation grace window.
                if (recovered && now >= DailyFuturesSession.FlattenUtc(today).AddMinutes(10)) completed = today;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error,"Daily futures flatten remains pending and will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
