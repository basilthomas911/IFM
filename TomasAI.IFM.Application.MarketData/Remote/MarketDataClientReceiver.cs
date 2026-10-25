using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Receives Host publications and reconciles local read views without owning provider connections or workers.</summary>
public sealed class MarketDataClientReceiver(MarketDataHostConnection connection, IMarketDataApi api,
    MarketDataReadMirror mirror, MarketDataRemoteAdmission admissions, ITickAggregationEventPublisher publisher,
    IMarketDataOperationsRecorder recorder, IActorSupervisor supervisor,
    ILogger<MarketDataClientReceiver> logger, Recovery.OptionFeedProcessingHealth? processingHealth = null) : BackgroundService
{
    readonly DatasetWorkerAdmissionRegistry futures = admissions.Futures;
    readonly DatasetWorkerAdmissionRegistry options = admissions.Options;
    readonly Dictionary<string, DatasetWorkerAdmission> activeFutures = new(StringComparer.Ordinal);
    readonly Dictionary<string, DatasetWorkerAdmission> activeOptions = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hosted infrastructure starts before actor registration; never resolve an actor producer early.
        while (!supervisor.IsReady) await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken).ConfigureAwait(false);
        await publisher.StartAsync(stoppingToken).ConfigureAwait(false);
        var futureIngress = new DatasetPublicationIngress(futures, publisher, recorder, evidencePersistedByHost: true);
        var optionIngress = new DatasetPublicationIngress(options, publisher, recorder, evidencePersistedByHost: true);
        try { await Task.WhenAll(ReceiveAsync(), ReceiveOptionCacheAsync(), ReconcileAsync()).ConfigureAwait(false); }
        finally
        {
            mirror.Disconnect();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await publisher.StopAsync(deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
        }

        async Task ReceiveAsync()
        {
            var client = await connection.GetClientAsync(stoppingToken).ConfigureAwait(false);
            await foreach (var message in client.Connection.SubscribeAsync<DatasetPublicationEnvelope>(connection.Scope + ".publications.>",
                serializer: NatsMessagePackSerializer<DatasetPublicationEnvelope>.Default, cancellationToken: stoppingToken).ConfigureAwait(false))
            {
                if (message.Data is not { } frame) continue;
                var isOption = message.Subject.StartsWith(connection.Scope + ".publications.Options.", StringComparison.Ordinal);
                try
                {
                    if (!isOption && !mirror.Accept(frame)) continue;
                    var accepted = await (isOption ? optionIngress : futureIngress).AcceptAsync(frame, stoppingToken).ConfigureAwait(false);
                    if (isOption && !accepted && frame.Kind == DatasetPublicationKind.OptionQuoteObservation)
                    {
                        var tick = MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(frame.Payload).OptionMarketPriceObservation!.OptionTickData;
                        logger.LogDebug("Option observation not admitted; Contract={Contract}; Generation={Generation}; QuoteUtc={QuoteUtc}; RiskUtc={RiskUtc}; PublicationSequence={Sequence}",
                            tick.ContractId, frame.GenerationId, tick.ObservedAtUtc, tick.CalculatedAtUtc, frame.PublicationSequence);
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                {
                    if (isOption) processingHealth?.Report(frame.ValueDate, frame.GenerationId, "ApiOptionIngress", error, stoppingToken);
                    logger.LogWarning(error, "MarketData Host publication could not be admitted; Dataset={Dataset}", frame.Dataset);
                }
            }
        }

        async Task ReceiveOptionCacheAsync()
        {
            var client = await connection.GetClientAsync(stoppingToken).ConfigureAwait(false);
            var assembler = new OptionChainCache.OptionChainPublicationAssembler();
            await foreach (var message in client.Connection.SubscribeAsync<OptionChainCache.OptionChainPublicationFrame>(connection.Scope + ".option-cache",
                serializer: NatsMessagePackSerializer<OptionChainCache.OptionChainPublicationFrame>.Default, cancellationToken: stoppingToken).ConfigureAwait(false))
            {
                if (message.Data is not { } frame || !mirror.NeedsOption(frame.GenerationId, frame.Scope, frame.Version)) continue;
                try { if (assembler.Accept(frame) is { } publication) mirror.AcceptOption(publication); }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                {
                    if (mirror.GetRuntimeStatus().ActiveValueDate is { } date) processingHealth?.Report(date, frame.GenerationId, "OptionCacheIngress", error, stoppingToken);
                    logger.LogWarning(error, "Invalid option-cache publication ignored.");
                }
            }
        }

        async Task ReconcileAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(3));
                    var snapshot = await api.GetBackendReadSnapshotAsync(deadline.Token).ConfigureAwait(false);
                    if (!mirror.Apply(snapshot)) continue;
                    Sync(futures, activeFutures, snapshot, "Futures");
                    Sync(options, activeOptions, snapshot, "Options");
                    // Live delivery and catch-up share ingress serialization and per-contract ordering.
                    // Repeated snapshots are harmless and never rewrite exchange/calculation timestamps.
                    foreach (var observation in snapshot.OptionObservations ?? [])
                    {
                        try
                        {
                            if (await optionIngress.AcceptAsync(observation, deadline.Token).ConfigureAwait(false))
                            {
                                var tick = MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(observation.Payload).OptionMarketPriceObservation!.OptionTickData;
                                logger.LogInformation("Option catch-up delivered; Contract={Contract}; Generation={Generation}; QuoteUtc={QuoteUtc}; RiskUtc={RiskUtc}; PublicationSequence={Sequence}",
                                    tick.ContractId, observation.GenerationId, tick.ObservedAtUtc, tick.CalculatedAtUtc, observation.PublicationSequence);
                            }
                        }
                        catch (Exception error) when (!deadline.IsCancellationRequested)
                        {
                            processingHealth?.Report(observation.ValueDate, observation.GenerationId, "OptionCatchUp", error, stoppingToken);
                            logger.LogWarning(error, "Option catch-up failed; retained value remains available for retry; Generation={Generation}", observation.GenerationId);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    foreach (var active in activeOptions.Values) processingHealth?.Report(active.ValueDate, active.GenerationId, "HostSynchronization", error, stoppingToken);
                    mirror.Disconnect();
                    foreach (var entry in activeFutures.Values) futures.Close(entry.Dataset, entry.GenerationId);
                    foreach (var entry in activeOptions.Values) options.Close(entry.Dataset, entry.GenerationId);
                    activeFutures.Clear(); activeOptions.Clear();
                    logger.LogWarning(error, "MarketData Host cache synchronization unavailable; read view is fenced.");
                }
                try { if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) break; }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    static void Sync(DatasetWorkerAdmissionRegistry admissions, Dictionary<string, DatasetWorkerAdmission> active,
        MarketDataReadSnapshot snapshot, string role)
    {
        var next = snapshot.Status.Datasets.Where(dataset => dataset.Role == role && dataset.Lifecycle == "Healthy" && dataset.ValueDate is not null)
            .ToDictionary(dataset => dataset.Dataset, dataset => new DatasetWorkerAdmission(dataset.Dataset, dataset.ValueDate!.Value,
                dataset.WorkerInstanceId, dataset.GenerationId, dataset.ManifestRevision), StringComparer.Ordinal);
        foreach (var pair in active)
            if (!next.TryGetValue(pair.Key, out var replacement) || replacement != pair.Value) admissions.Close(pair.Key, pair.Value.GenerationId);
        foreach (var pair in next) admissions.Admit(pair.Value);
        active.Clear(); foreach (var pair in next) active.Add(pair.Key, pair.Value);
    }
}
