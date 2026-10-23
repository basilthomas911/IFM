using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Relays terminal dataset recovery outcomes through the existing desktop event channel.</summary>
public sealed class OptionFeedRecoveryNotificationService(IMarketDataApi market, IJSActorProducer producer,
    ILogger<OptionFeedRecoveryNotificationService> logger, IActorSupervisor? supervisor = null,
    IFuturesMarketSessionAuthority? sessions = null, TimeProvider? timeProvider = null,
    TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.OptionPipelineRecoveryCoordinator? recovery = null,
    ApiRecoveryRestartEscalation? escalation = null) : BackgroundService
{
    readonly OptionPipelineFailureObserver pipeline = new(timeProvider ?? TimeProvider.System);
    readonly Dictionary<string, (Guid Worker, Guid Generation, string State)> published = new(StringComparer.Ordinal);
    readonly Dictionary<string, DateTime> lastPublished = new(StringComparer.Ordinal);
    readonly SemaphoreSlim producerStart = new(1, 1);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await Task.WhenAll(ObserveDatasetsAsync(stoppingToken), ObservePipelineFailuresAsync(stoppingToken)).ConfigureAwait(false);

    async Task EnsureProducerStartedAsync(CancellationToken token)
    {
        await producerStart.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!producer.IsRunning)
                await producer.StartAsync(new(ActorType.Event, nameof(OptionFeedRecoveryNotificationService)), token).ConfigureAwait(false);
        }
        finally { producerStart.Release(); }
    }

    async Task ObservePipelineFailuresAsync(CancellationToken stoppingToken)
    {
        if (recovery is null) return;
        Guid sentOperation = default; DateTime sentAt = default;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            do
            {
                try
                {
                    if (recovery.Failure is not { } terminal || terminal.CommandId == sentOperation && DateTime.UtcNow - sentAt < TimeSpan.FromSeconds(10)) continue;
                    using var delivery = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    delivery.CancelAfter(TimeSpan.FromSeconds(3));
                    await EnsureProducerStartedAsync(delivery.Token).ConfigureAwait(false);
                    await producer.SendAsync<MarketDataDatasetRecoveryFailedEvent, MarketDataFeedId>(terminal.Subject, terminal, delivery.Token)
                        .AsTask().WaitAsync(delivery.Token).ConfigureAwait(false);
                    if (escalation is not null) await escalation.RequestAsync(terminal, delivery.Token).ConfigureAwait(false);
                    sentOperation = terminal.CommandId; sentAt = DateTime.UtcNow;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning(error, "Option pipeline error delivery failed; retained terminal failure will be retried."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    async Task ObserveDatasetsAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                await EnsureProducerStartedAsync(deadline.Token).ConfigureAwait(false);
                // Observe API actors before Host RPC: a healthy worker or an unavailable Host must not hide API quarantine.
                if (recovery is null && supervisor is not null && sessions is not null)
                {
                    var failures = pipeline.Observe(sessions.Current.ActiveValueDate,
                        supervisor.RuntimeContext.CaptureActorMetricsSources()
                            .Where(source => source.ActorId.ActorType == ActorType.Realtime
                                && source.ActorId.Name is "IronCondorTradePositionRealtime" or "FuturesOptionRealtime")
                            .SelectMany(source => source.CaptureSnapshot().Mailboxes));
                    foreach (var failed in failures)
                    {
                        await producer.SendAsync<MarketDataDatasetRecoveryFailedEvent, MarketDataFeedId>(failed.Subject, failed, deadline.Token)
                            .AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
                        pipeline.MarkPublished(failed.CommandId);
                        logger.LogError("Option pipeline recovery deadline exceeded; OperationId={OperationId}; Detail={Detail}", failed.CommandId, failed.ErrorMessage);
                    }
                }
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
