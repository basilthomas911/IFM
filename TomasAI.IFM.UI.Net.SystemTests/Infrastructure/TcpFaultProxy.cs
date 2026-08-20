using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>
/// Isolated loopback TCP proxy used to exercise client disconnect/reconnect behavior without stopping a shared service.
/// </summary>
public sealed class TcpFaultProxy : IAsyncDisposable
{
    readonly string _targetHost;
    readonly int _targetPort;
    readonly TcpListener _listener;
    readonly CancellationTokenSource _lifetime = new();
    readonly ConcurrentDictionary<long, ConnectionPair> _connections = new();
    readonly Task _acceptLoop;
    long _nextConnectionId;
    long _forwardedConnectionCount;
    int _paused;

    public TcpFaultProxy(string targetHost, int targetPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetPort);
        _targetHost = targetHost;
        _targetPort = targetPort;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptLoopAsync(_lifetime.Token);
    }

    public int Port { get; }
    public Uri Uri => new($"nats://127.0.0.1:{Port}");
    public long ForwardedConnectionCount => Interlocked.Read(ref _forwardedConnectionCount);
    public int ActiveConnectionCount => _connections.Count;
    public bool IsPaused => Volatile.Read(ref _paused) != 0;

    public void PauseAndDropConnections()
    {
        Volatile.Write(ref _paused, 1);
        foreach (var pair in _connections.Values)
            pair.Dispose();
    }

    public void Resume() => Volatile.Write(ref _paused, 0);

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _listener.Stop();
        foreach (var pair in _connections.Values)
            pair.Dispose();
        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        _lifetime.Dispose();
    }

    async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient incoming;
            try
            {
                incoming = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (IsPaused)
            {
                incoming.Dispose();
                continue;
            }

            _ = ForwardAsync(incoming, cancellationToken);
        }
    }

    async Task ForwardAsync(TcpClient incoming, CancellationToken cancellationToken)
    {
        TcpClient? outgoing = null;
        ConnectionPair? pair = null;
        var connectionId = Interlocked.Increment(ref _nextConnectionId);
        try
        {
            outgoing = new TcpClient();
            await outgoing.ConnectAsync(_targetHost, _targetPort, cancellationToken).ConfigureAwait(false);
            if (IsPaused)
                return;

            pair = new ConnectionPair(incoming, outgoing);
            if (!_connections.TryAdd(connectionId, pair))
                return;
            Interlocked.Increment(ref _forwardedConnectionCount);

            var upstream = incoming.GetStream().CopyToAsync(outgoing.GetStream(), cancellationToken);
            var downstream = outgoing.GetStream().CopyToAsync(incoming.GetStream(), cancellationToken);
            await Task.WhenAny(upstream, downstream).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            pair?.Dispose();
            if (pair is null)
            {
                incoming.Dispose();
                outgoing?.Dispose();
            }
        }
    }

    sealed class ConnectionPair(TcpClient incoming, TcpClient outgoing) : IDisposable
    {
        int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            incoming.Dispose();
            outgoing.Dispose();
        }
    }
}
