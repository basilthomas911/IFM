using System.Buffers;
using System.Threading.Channels;
using MessagePack;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Rust.Publication;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Passive copied-byte log receiver on its own broker connection.
/// It owns no native provider, lease, context, recovery or dataset command.
/// A stalled disk/logger cannot block the shared Host RPC connection or Rust cleanup.</summary>
public sealed class NativeProviderDiagnosticLogging(IConfiguration configuration, MarketDataHostConnection subjects,
    ILogger<NativeProviderDiagnosticLogging> logger) : BackgroundService
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
                    Name = "IFM.NativeMarketData.Diagnostics",
                    // This external log receiver has no authoritative input to
                    // drain; native feed cancellation never waits for its queue.
                    DrainSubscriptionsOnDispose = false
                });
                await client.ConnectAsync().AsTask().WaitAsync(stoppingToken).ConfigureAwait(false);
                await using var subscriber = await client.Connection.SubscribeCoreAsync<NativeProviderDiagnostic>(
                    subjects.Scope + ".diagnostics.>", serializer: Plain<NativeProviderDiagnostic>.Default,
                    opts: new NatsSubOpts { ChannelOpts = new NatsSubChannelOpts { Capacity = 256, FullMode = BoundedChannelFullMode.Wait } },
                    cancellationToken: stoppingToken).ConfigureAwait(false);
                await foreach (var message in subscriber.Msgs.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                {
                    var value = message.Data;
                    if (value is null || value.Worker == Guid.Empty || value.Attempt == 0 || value.Kind is < 1 or > 2
                        || value.Code > 255 || value.ObservedAtUtc.Offset != TimeSpan.Zero || value.Message is null || value.Message.Length > 4096
                        || value.Dataset is not ("GLBX.MDP3" or "XCBF.PITCH") || value.Role is not ("Options" or "Futures"))
                    {
                        logger.LogWarning("Rejected malformed native provider diagnostic; Subject={Subject}", message.Subject);
                        continue;
                    }
                    var level = value.Kind == 2 ? LogLevel.Error : LogLevel.Information;
                    logger.Log(level, "Databento diagnostic: Dataset={Dataset}; Role={Role}; Worker={Worker}; Attempt={Attempt}; ObservedAtUtc={ObservedAtUtc}; Kind={Kind}; Code={Code}; IsLast={IsLast}; Replaced={Replaced}; Message={ProviderMessage}",
                        value.Dataset, value.Role, value.Worker, value.Attempt, value.ObservedAtUtc, value.Kind, value.Code, value.IsLast, value.Replaced, value.Message);
                    if (value.Replaced != 0)
                        logger.LogWarning("Native provider diagnostic retention replaced {Replaced} older records; Dataset={Dataset}; Role={Role}; Worker={Worker}",
                            value.Replaced, value.Dataset, value.Role, value.Worker);
                    if (!string.IsNullOrWhiteSpace(message.ReplyTo))
                    {
                        // Acknowledge the exact copied record only after the
                        // logger accepted it. This is not a broker-only PONG,
                        // physical disk durability or a displayed UI dialog.
                        var bytes = MessagePackSerializer.Serialize(value, Plain<NativeProviderDiagnostic>.Options);
                        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
                        await message.ReplyAsync(new NativeDiagnosticReceipt(1, digest, digest, true, null),
                            serializer: Plain<NativeDiagnosticReceipt>.Default, cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                // Known external log-transport retry. It does not own or change
                // a Rust dataset's recovery window or readiness proofs.
                logger.LogWarning(error, "Native provider diagnostic receiver disconnected; retrying its independent log connection");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    sealed class Plain<T> : INatsSerializer<T>
    {
        internal static Plain<T> Default { get; } = new();
        internal static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);
        public T? Deserialize(in ReadOnlySequence<byte> buffer)
        {
            if (buffer.Length > 8192) throw new InvalidDataException("Native provider diagnostic exceeds its bounded wire size.");
            return MessagePackSerializer.Deserialize<T>(buffer, Options);
        }
        public void Serialize(IBufferWriter<byte> writer, T value) => MessagePackSerializer.Serialize(writer, value, Options);
        public INatsSerializer<T> CombineWith(INatsSerializer<T> next) => throw new NotSupportedException();
    }
}
