using System.Buffers;
using System.Threading.Channels;
using MessagePack;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Rust.Persistence;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Bounded raw database fact endpoint for the revised native owner.
/// It is deliberately separate from the native command/control subscribers.
/// A blocked database job holds copied bytes only; it cannot retain native leases.</summary>
public sealed class NativeReferenceStorageService(IConfiguration configuration, MarketDataHostConnection connection,
    NativeReferenceStorage storage, ILogger<NativeReferenceStorageService> logger) : BackgroundService
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
                    Name = "IFM.NativeMarketData.References",
                    DrainSubscriptionsOnDispose = false
                });
                await client.ConnectAsync().AsTask().WaitAsync(stoppingToken).ConfigureAwait(false);
                await using var subscriber = await client.Connection.SubscribeCoreAsync<NativeReferenceRequest>(
                    connection.Scope + ".reference.read", serializer: Plain<NativeReferenceRequest>.Default,
                    // A dropped IO request remains bounded by the native request deadline.
                    // Backpressure here must not block the dedicated reference connection or shared Host control connection.
                    opts: new NatsSubOpts { ChannelOpts = new NatsSubChannelOpts { Capacity = 64, FullMode = BoundedChannelFullMode.DropWrite } },
                    cancellationToken: stoppingToken).ConfigureAwait(false);
                await Parallel.ForEachAsync(subscriber.Msgs.ReadAllAsync(stoppingToken),
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = stoppingToken },
                    async (message, token) =>
                    {
                        var request = message.Data;
                        if (request is null || string.IsNullOrWhiteSpace(message.ReplyTo)) return;
                        NativeReferencePage receipt;
                        try { receipt = await storage.ReadAsync(request, token).ConfigureAwait(false); }
                        catch (Exception error) when (!token.IsCancellationRequested)
                        {
                            logger.LogError(error, "Native raw-reference IO failed; Request={Request}; Kind={Kind}", request.Id, request.Kind);
                            // The full exception stays in structured logs. Untrusted source
                            // or database diagnostics are not echoed onto broker subjects.
                            receipt = new(request.Id, 0, 0, "", [], "Raw reference database operation failed.");
                        }
                        await message.ReplyAsync(receipt, serializer: Plain<NativeReferencePage>.Default,
                            cancellationToken: token).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                // Retry the external copied-byte endpoint. A failed consumer
                // never acquires or repairs a native dataset resource.
                logger.LogWarning(error, "Native raw-reference receiver disconnected; retrying independent storage transport");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
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
