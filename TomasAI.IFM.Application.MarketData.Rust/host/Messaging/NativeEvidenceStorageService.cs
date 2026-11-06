using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MessagePack;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Persistence;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Bounded external persistence endpoint for the revised native owner.
/// It is deliberately separate from the native command/control subscribers.
/// A blocked database job holds copied bytes only; it cannot retain native leases.</summary>
public sealed class NativeEvidenceStorageService(IConfiguration configuration, MarketDataHostConnection connection,
    NativeEvidenceStorage storage, ILogger<NativeEvidenceStorageService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var client = new NatsClient(new NatsOpts
                {
                    Url = configuration["MarketDataHost:NatsUrl"] ?? "nats://127.0.0.1:4222",
                    Name = "IFM.NativeMarketData.Evidence",
                    DrainSubscriptionsOnDispose = false
                });
                await client.ConnectAsync().AsTask().WaitAsync(stoppingToken).ConfigureAwait(false);
                await using var subscriber = await client.Connection.SubscribeCoreAsync<NativeEvidenceWrite>(
                    connection.Scope + ".evidence.write", serializer: Plain<NativeEvidenceWrite>.Default,
                    // A dropped IO request remains in Rust's immutable evidence outbox.
                    // Backpressure here must not block the dedicated evidence connection or shared Host control connection.
                    opts: new NatsSubOpts { ChannelOpts = new NatsSubChannelOpts { Capacity = 64, FullMode = BoundedChannelFullMode.DropWrite } },
                    cancellationToken: stoppingToken).ConfigureAwait(false);
                await Parallel.ForEachAsync(ReadBatchesAsync(subscriber.Msgs, stoppingToken),
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = stoppingToken },
                    async (batch, token) =>
                    {
                        var results = await storage.WriteBatchAsync(batch.Select(static item => item.Request).ToArray(), token)
                            .ConfigureAwait(false);
                        for (var index = 0; index < batch.Count; index++)
                        {
                            var (message, request) = batch[index];
                            var (receipt, error) = results[index];
                            if (error is not null)
                                logger.LogError(error, "Native copied evidence persistence failed; Receipt={Receipt}; Source={Source}",
                                    request.Receipt, request.Identity);
                            await message.ReplyAsync(receipt, serializer: Plain<NativeEvidenceReceipt>.Default,
                                cancellationToken: token).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                // Retry the external copied-byte endpoint. A failed consumer
                // never acquires or repairs a native dataset resource.
                logger.LogWarning(error, "Native evidence receiver disconnected; retrying independent storage transport");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    // Drain only requests already available so quiet feeds incur no batching timer.
    static async IAsyncEnumerable<List<(NatsMsg<NativeEvidenceWrite> Message, NativeEvidenceWrite Request)>> ReadBatchesAsync(
        ChannelReader<NatsMsg<NativeEvidenceWrite>> messages,
        [EnumeratorCancellation] CancellationToken stoppingToken)
    {
        const int capacity = IOptionTradeEvidenceWriter.MaximumFirstWriteBatch;
        while (await messages.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            var batch = new List<(NatsMsg<NativeEvidenceWrite>, NativeEvidenceWrite)>(capacity);
            for (var read = 0; read < capacity && messages.TryRead(out var message); read++)
                if (message.Data is { } request && !string.IsNullOrWhiteSpace(message.ReplyTo))
                    batch.Add((message, request));
            if (batch.Count != 0)
                yield return batch;
        }
    }

    // Plain bounded MessagePack is explicit on this new internal IO protocol.
    // Existing public event/envelope serializers and consumers remain unchanged.
    sealed class Plain<T> : INatsSerializer<T>
    {
        public static Plain<T> Default { get; } = new();
        static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);
        public T? Deserialize(in ReadOnlySequence<byte> buffer)
        {
            if (buffer.Length > 132096) throw new InvalidDataException("Evidence handoff exceeds capacity.");
            return MessagePackSerializer.Deserialize<T>(buffer, Options);
        }
        public void Serialize(IBufferWriter<byte> writer, T value) => MessagePackSerializer.Serialize(writer, value, Options);
        public INatsSerializer<T> CombineWith(INatsSerializer<T> next) => throw new NotSupportedException();
    }
}
