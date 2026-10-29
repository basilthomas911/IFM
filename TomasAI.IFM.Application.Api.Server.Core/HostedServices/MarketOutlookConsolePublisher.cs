using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Writes Outlook refresh notices on the backend, independently of any
/// UI. Console delivery never waits on or blocks market-data processing.</summary>
public sealed class MarketOutlookConsolePublisher(IMarketOutlookSnapshotPublisher inner,
    IStatusConsoleEventProducer console, TimeProvider clock, ILogger<MarketOutlookConsolePublisher> logger)
    : BackgroundService, IMarketOutlookSnapshotPublisher
{
    // The durable console stream supplies its existing five-minute catch-up
    // window. This small staging queue cannot grow during a broker outage.
    readonly Channel<StatusConsoleLoggedEvent> pending = Channel.CreateBounded<StatusConsoleLoggedEvent>(
        new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    readonly object noticeGate = new();
    readonly Dictionary<string, (DateOnly Date, string Message)> lastNotices = new(StringComparer.Ordinal);

    public async ValueTask PublishAsync(MarketOutlookUpdate update, MarketOutlookReadModel snapshot, CancellationToken cancellationToken)
    {
        await inner.PublishAsync(update, snapshot, cancellationToken).ConfigureAwait(false);
        var id = update.UpdateId == Guid.Empty ? Guid.NewGuid() : update.UpdateId;
        var at = snapshot.UpdatedAtUtc.Kind == DateTimeKind.Utc ? snapshot.UpdatedAtUtc : DateTime.SpecifyKind(snapshot.UpdatedAtUtc, DateTimeKind.Utc);
        var source = LogSourceType.MarketDataFeedEvent;
        var price = snapshot.FuturesEodData.ClosePrice > 0 ? snapshot.FuturesEodData.ClosePrice.ToString("0.00", CultureInfo.InvariantCulture) : "N/A";
        var message = $"{snapshot.ContractId} Market Outlook refreshed by {snapshot.RefreshTrigger} @ {price}";
        // Several component snapshots can describe the same console notice.
        // Deduplicate the displayed content, never the underlying market updates.
        // Keep only the last notice per contract, with a hard bound across rollovers.
        lock (noticeGate)
        {
            var key = (snapshot.ValueDate, message);
            if (lastNotices.TryGetValue(snapshot.ContractId, out var previous) && previous == key)
                return;
            if (lastNotices.Count >= 128 && !lastNotices.ContainsKey(snapshot.ContractId))
                lastNotices.Remove(lastNotices.Keys.First());
            lastNotices[snapshot.ContractId] = key;
            pending.Writer.TryWrite(new StatusConsoleLoggedEvent {
                Id = id, CommandId = id,
                Subject = new ActorSubject(ActorType.Notify, StatusConsoleLoggedEvent.Actor, StatusConsoleLoggedEvent.Verb, source.ToString()),
                EntityId = new ActorEntityId(source.ToString()),
                StatusConsoleLog = new StatusConsoleLogReadModel(at, 0, source,
                    message)
            });
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var notice in pending.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                while (clock.GetUtcNow().UtcDateTime - notice.StatusConsoleLog.StatusDate < TimeSpan.FromMinutes(5))
                {
                    try
                    {
                        if (!console.IsRunning)
                            await console.StartAsync(notice.Subject.ActorId, stoppingToken).AsTask().WaitAsync(stoppingToken).ConfigureAwait(false);
                        // Retain the same event identity on retry. Broker ACK is
                        // storage acknowledgement; rendering uses existing UI ACKs.
                        await console.PublishConsoleAsync(notice).AsTask().WaitAsync(stoppingToken).ConfigureAwait(false);
                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        logger.LogWarning(error, "Market Outlook console delivery failed; retained notice will retry; NoticeId={NoticeId}", notice.Id);
                        await Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
