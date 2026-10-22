using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Relays terminal dataset recovery outcomes through the existing desktop event channel.</summary>
public sealed class OptionFeedRecoveryNotificationService(IMarketDataApi market, IJSActorProducer producer,
    ILogger<OptionFeedRecoveryNotificationService> logger) : BackgroundService
{
    readonly Dictionary<string, (Guid Worker, Guid Generation, string State)> published = new(StringComparer.Ordinal);
    readonly Dictionary<string, DateTime> lastPublished = new(StringComparer.Ordinal);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                if (!producer.IsRunning)
                    await producer.StartAsync(new(ActorType.Event, nameof(OptionFeedRecoveryNotificationService)), deadline.Token).ConfigureAwait(false);
                var status = await market.GetBackendStatusAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
                foreach (var dataset in status.Datasets.Where(dataset => (dataset.Role is "Options" or "Futures")
                    && dataset.Lifecycle is "StoppedError" or "CleanupUnconfirmed"))
                {
                    var key = dataset.Role + "|" + dataset.Dataset;
                    var identity = (dataset.WorkerInstanceId, dataset.GenerationId, dataset.Lifecycle);
                    if (published.TryGetValue(key, out var previous) && previous == identity && DateTime.UtcNow - lastPublished[key] < TimeSpan.FromSeconds(10)) continue;
                    if (dataset.Role == "Options")
                    {
                        var failed = CreateEvent(dataset);
                        await producer.SendAsync<OptionMarketDataFeedFailedEvent, MarketDataFeedId>(failed.Subject, failed, deadline.Token)
                            .AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        var failed = CreateDatasetEvent(dataset);
                        await producer.SendAsync<MarketDataDatasetRecoveryFailedEvent, MarketDataFeedId>(failed.Subject, failed, deadline.Token)
                            .AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
                    }
                    published[key] = identity;
                    lastPublished[key] = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning(error, "Option-feed recovery status could not be relayed; next observation will retry."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try { await base.StopAsync(cancellationToken).ConfigureAwait(false); }
        finally { await producer.StopAsync(cancellationToken).ConfigureAwait(false); }
    }

    /// <summary>Creates a UI error preserving the failed dataset, operation and recovery phase.</summary>
    public static MarketDataDatasetRecoveryFailedEvent CreateDatasetEvent(MarketDataDatasetStatus dataset)
    {
        var id = new MarketDataFeedId(dataset.ValueDate ?? throw new InvalidDataException("Dataset status requires its value date."));
        return new()
        {
            Subject = new(ActorType.Event, MarketDataDatasetRecoveryFailedEvent.Actor, MarketDataDatasetRecoveryFailedEvent.Verb, id.Format()),
            EntityId = id, Id = Guid.NewGuid(), CommandId = dataset.OperationId,
            ErrorDate = dataset.ChangedAtUtc == default ? DateTime.UtcNow : dataset.ChangedAtUtc.UtcDateTime,
            ReceivedOn = DateTime.UtcNow, EventSource = nameof(OptionFeedRecoveryNotificationService),
            ErrorCode = 503, ErrorType = ErrorType.System,
            ErrorMessage = $"{dataset.Role}/{dataset.Dataset}: {dataset.Error ?? "Dataset recovery failed."}",
            Dataset = dataset.Dataset, Role = dataset.Role, WorkerInstanceId = dataset.WorkerInstanceId, GenerationId = dataset.GenerationId
        };
    }

    public static OptionMarketDataFeedFailedEvent CreateEvent(MarketDataDatasetStatus dataset)
    {
        var id = new MarketDataFeedId(dataset.ValueDate ?? throw new InvalidDataException("Option status requires its value date."));
        return new()
        {
            Subject = new(ActorType.Event, OptionMarketDataFeedFailedEvent.Actor, OptionMarketDataFeedFailedEvent.Verb, id.Format()),
            EntityId = id, Id = Guid.NewGuid(), CommandId = dataset.OperationId, ErrorDate = dataset.ChangedAtUtc == default ? DateTime.UtcNow : dataset.ChangedAtUtc.UtcDateTime,
            ReceivedOn = DateTime.UtcNow, EventSource = nameof(OptionFeedRecoveryNotificationService),
            ErrorCode = 503, ErrorType = ErrorType.System, ErrorMessage = dataset.Error ?? "Option dataset recovery failed.",
            Dataset = dataset.Dataset, WorkerInstanceId = dataset.WorkerInstanceId, GenerationId = dataset.GenerationId,
            CleanupConfirmed = dataset.Lifecycle == "StoppedError" && !dataset.Running
        };
    }
}
