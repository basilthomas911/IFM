using System.Collections.Concurrent;
using MessagePack;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Application.MarketData.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Independent storage workers; no raw history enters an optional actor mailbox.</summary>
public sealed class RawFuturesStorageConsumer(RawFuturesStorageOptions options, RawFuturesStorageTransport transport,
    IDbContextFactory databases, ILogger<RawFuturesStorageConsumer> logger) : BackgroundService, IHealthCheck
{
    readonly ConcurrentDictionary<string, string> failures = new(StringComparer.Ordinal);
    volatile bool ready;
    string? connectionFailure;
    long stored;
    volatile bool failureOverflow;
    INatsJSConsumer? currentConsumer;
    public long StoredRecords => Interlocked.Read(ref stored);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) { ready = true; return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var js = await transport.GetAsync(stoppingToken).ConfigureAwait(false);
                var consumer = await js.CreateOrUpdateConsumerAsync(options.Stream, new ConsumerConfig("raw_rows_v1")
                {
                    FilterSubject = options.Subject, AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    DeliverPolicy = ConsumerConfigDeliverPolicy.All, AckWait = TimeSpan.FromSeconds(30),
                    MaxDeliver = -1, MaxAckPending = 16
                }, stoppingToken).ConfigureAwait(false);
                connectionFailure = null; ready = true;
                currentConsumer = consumer;
                await Parallel.ForEachAsync(consumer.ConsumeAsync(opts: new NatsJSConsumeOpts { MaxMsgs = 8, ThresholdMsgs = 4 },
                    serializer: new NatsByteArrayMessageSerializer(), cancellationToken: stoppingToken),
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = stoppingToken }, async (message, token) =>
                    {
                        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(message.Data ?? []));
                        try
                        {
                            var batch = MessagePackSerializer.Deserialize<RawFuturesStorageBatch>(message.Data ?? throw new InvalidDataException("Missing raw storage payload."), cancellationToken: token);
                            await databases.TradeTickQuoteDb.DbWriter.InsertRawFuturesBatchAsync(batch, token).ConfigureAwait(false);
                            await message.AckAsync(cancellationToken: token).ConfigureAwait(false);
                            failures.TryRemove(key, out _); Interlocked.Add(ref stored, batch.Records.Length);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                        catch (Exception error)
                        {
                            if (failures.Count < 256) failures[key] = error.Message;
                            else failureOverflow = true;
                            logger.LogError(error, "Raw futures storage failed; durable batch remains unacknowledged");
                            await message.NakAsync(delay: TimeSpan.FromSeconds(1), cancellationToken: token).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                connectionFailure = error.Message; ready = false;
                logger.LogError(error, "Raw futures storage consumer unavailable; durable source and broker history are retained");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (options.Enabled && currentConsumer is { } consumer)
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(1));
                if (options.RequireSourceHealth && !await transport.SourceIsHealthyAsync(deadline.Token).ConfigureAwait(false))
                    return HealthCheckResult.Unhealthy("Native raw journal publisher has an unresolved source recovery failure.");
                await consumer.RefreshAsync(deadline.Token).ConfigureAwait(false);
                if (consumer.Info.NumPending == 0 && consumer.Info.NumAckPending == 0)
                { failureOverflow = false; failures.Clear(); }
            }
            catch (Exception error) { return HealthCheckResult.Unhealthy("Raw storage broker status unavailable.", error); }
        }
        return !options.Enabled || ready && connectionFailure is null && failures.IsEmpty && !failureOverflow
            ? HealthCheckResult.Healthy("Durable raw futures storage consumer is available.", new Dictionary<string, object> { ["storedRecords"] = StoredRecords })
            : HealthCheckResult.Unhealthy(connectionFailure ?? failures.Values.FirstOrDefault() ?? "Raw storage is starting or has unconfirmed failed deliveries.");
    }
}
