using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Application.MarketData.Storage;

/// <summary>Dedicated bounded file-backed stream. Full storage rejects publication instead of discarding history.</summary>
public sealed class RawFuturesStorageTransport(RawFuturesStorageOptions options, NatsConnectionManager connections)
{
    readonly SemaphoreSlim gate = new(1, 1);
    INatsJSContext? context;
    public async Task<INatsJSContext> GetAsync(CancellationToken token)
    {
        if (context is { } existing) return existing;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (context is not null) return context;
            options.Validate();
            var js = await connections.GetJetStreamContextAsync(options.NatsUrl, token).ConfigureAwait(false);
            await js.CreateOrUpdateStreamAsync(new StreamConfig(options.Stream, [options.Subject])
            {
                Storage = StreamConfigStorage.File, Retention = StreamConfigRetention.Workqueue,
                Discard = StreamConfigDiscard.New, MaxBytes = 8L * 1024 * 1024 * 1024,
                MaxMsgSize = 64_000
            }, token).ConfigureAwait(false);
            context = js;
            return js;
        }
        finally { gate.Release(); }
    }
    public async Task PublishAsync(byte[] payload, string identity, CancellationToken token)
    {
        if (payload.Length > 64_000) throw new InvalidDataException("Raw storage message exceeds byte limit.");
        var js = await GetAsync(token).ConfigureAwait(false);
        var result = await js.PublishAsync(options.Subject, payload, serializer: new NatsByteArrayMessageSerializer(),
            headers: new NatsHeaders { { "Nats-Msg-Id", identity } }, cancellationToken: token).ConfigureAwait(false);
        if (result.Error is not null) throw new InvalidOperationException(result.Error.Description);
    }
    public async Task RespondToHealthAsync(Func<bool> ready, CancellationToken token)
    {
        var client = await connections.GetClientAsync(options.NatsUrl, token).ConfigureAwait(false);
        var serializer = new NatsByteArrayMessageSerializer();
        await using var subscription = await client.Connection.SubscribeCoreAsync<byte[]>(options.Subject + ".source-health", serializer: serializer,
            cancellationToken: token).ConfigureAwait(false);
        await foreach (var message in subscription.Msgs.ReadAllAsync(token).ConfigureAwait(false))
            if (!string.IsNullOrWhiteSpace(message.ReplyTo))
                await message.ReplyAsync(new byte[] { ready() ? (byte)1 : (byte)0 }, serializer: serializer, cancellationToken: token).ConfigureAwait(false);
    }
    public async Task<bool> SourceIsHealthyAsync(CancellationToken token)
    {
        var client = await connections.GetClientAsync(options.NatsUrl, token).ConfigureAwait(false);
        var serializer = new NatsByteArrayMessageSerializer();
        var result = await client.RequestAsync<byte[], byte[]>(options.Subject + ".source-health", [], requestSerializer: serializer,
            replySerializer: serializer, cancellationToken: token).ConfigureAwait(false);
        return result.Data is [1];
    }
}
