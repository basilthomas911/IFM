using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Net;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Owns the process-level NATS connection shared by publishers, subscribers, and JetStream contexts.
/// </summary>
/// <remarks>
/// NATS multiplexes subscriptions and publishers over one TCP connection. Keeping that connection at
/// application scope avoids repeated sockets, reconnect loops, reader/writer tasks, and protocol buffers.
/// </remarks>
public sealed class NatsConnectionManager : IAsyncDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    NatsClient? _client;
    INatsJSContext? _jetStream;
    string? _url;
    int _disposeState;
    bool _disposed;

    public async ValueTask<NatsClient> GetClientAsync(string url, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Volatile.Read(ref _client) is { } existing)
        {
            EnsureSameUrl(url);
            return existing;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_client is not null)
            {
                EnsureSameUrl(url);
                return _client;
            }

            var options = new NatsOpts
            {
                Url = url,
                Name = "TomasAI.IFM",
                RequestTimeout = TimeSpan.FromMinutes(2),
                CommandTimeout = TimeSpan.FromMinutes(2),
                DrainSubscriptionsOnDispose = true,
                ConsumerDrainOnDisposeTimeout = TimeSpan.FromSeconds(30)
            };
            var client = new NatsClient(options);
            await client.ConnectAsync().ConfigureAwait(false);
            _url = url;
            Volatile.Write(ref _client, client);
            return client;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<INatsJSContext> GetJetStreamContextAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _jetStream) is { } existing)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureSameUrl(url);
            return existing;
        }

        var client = await GetClientAsync(url, cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _jetStream ??= client.CreateJetStreamContext();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;
        Volatile.Write(ref _disposed, true);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _jetStream = null;
            if (_client is not null)
            {
                await _client.DisposeAsync().ConfigureAwait(false);
                _client = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    void EnsureSameUrl(string url)
    {
        if (!string.Equals(_url, url, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"The shared NATS connection is already bound to '{_url}' and cannot also connect to '{url}'.");
    }
}
