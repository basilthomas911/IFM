using StackExchange.Redis;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

/// <summary>Owns one asynchronously connected multiplexer without blocking service resolution or unrelated startup.</summary>
public sealed class RedisConnectionStartup : IHostedService, IAsyncDisposable
{
    readonly Func<Task<IConnectionMultiplexer>> connect;
    readonly ILogger<RedisConnectionStartup> logger;
    readonly object gate = new();
    Task<IConnectionMultiplexer>? pending;
    bool disposed;
    int connectionDisposed;

    public RedisConnectionStartup(string configuration, ILogger<RedisConnectionStartup> logger)
        : this(async () => await ConnectionMultiplexer.ConnectAsync(ConfigurationOptions.Parse(configuration)), logger) { }

    internal RedisConnectionStartup(Func<Task<IConnectionMultiplexer>> connect, ILogger<RedisConnectionStartup> logger)
    { this.connect = connect; this.logger = logger; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = ObserveAsync();
        return Task.CompletedTask;
    }

    async Task ObserveAsync()
    {
        try { _ = await GetAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception error) { logger.LogError(error, "Redis startup connection failed; dependent probes report unavailable while independent startup continues."); }
    }

    public Task<IConnectionMultiplexer> GetAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task<IConnectionMultiplexer> current;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (pending is null || pending.IsFaulted || pending.IsCanceled) pending = ConnectOwnedAsync();
            current = pending;
        }
        return current.WaitAsync(token);
    }

    /// <summary>Supports existing synchronous cache operations after async readiness; never waits or starts a synchronous connection.</summary>
    internal bool TryGetReady(out IConnectionMultiplexer? connection)
    {
        lock (gate)
        {
            connection = !disposed && pending is { IsCompletedSuccessfully: true } ? pending.Result : null;
            return connection?.IsConnected == true;
        }
    }

    async Task<IConnectionMultiplexer> ConnectOwnedAsync()
    {
        var connection = await connect().ConfigureAwait(false);
        lock (gate)
        {
            if (!disposed) return connection;
        }
        await DisposeConnectionAsync(connection).ConfigureAwait(false);
        throw new ObjectDisposedException(nameof(RedisConnectionStartup));
    }

    async ValueTask DisposeConnectionAsync(IConnectionMultiplexer connection)
    {
        if (Interlocked.Exchange(ref connectionDisposed, 1) == 0)
            await connection.DisposeAsync().ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask().WaitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        Task<IConnectionMultiplexer>? current;
        lock (gate) { if (disposed) return; disposed = true; current = pending; }
        // An outstanding ConnectAsync owns late disposal; host shutdown does not wait on a slow endpoint.
        if (current is { IsCompletedSuccessfully: true })
            await DisposeConnectionAsync(current.Result).ConfigureAwait(false);
        else if (current is not null) _ = DisposeLateAsync(current);
    }

    async Task DisposeLateAsync(Task<IConnectionMultiplexer> current)
    {
        try { await DisposeConnectionAsync(await current.ConfigureAwait(false)).ConfigureAwait(false); }
        catch (Exception error) { logger.LogDebug(error, "Redis connection finished after shutdown without an active connection."); }
    }
}
