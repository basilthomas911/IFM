using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class RedisConnectionStartupTests
{
    [Fact]
    public async Task Cache_adapter_resolves_without_connecting_and_preserves_sync_and_async_operations_after_readiness()
    {
        var pending = new TaskCompletionSource<IConnectionMultiplexer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        connection.IsConnected.Returns(true);
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        database.StringGet(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns((RedisValue)"ready");
        database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult((RedisValue)"ready"));
        await using var startup = new RedisConnectionStartup(() => pending.Task, NullLogger<RedisConnectionStartup>.Instance);
        var cache = new StartupRedisCache(startup);
        Assert.Throws<InvalidOperationException>(() => cache.Get("key"));
        Assert.Throws<InvalidOperationException>(() => cache.Set("key", "value"));
        var read = cache.GetAsync("key"); Assert.False(read.IsCompleted);
        pending.SetResult(connection);
        Assert.Equal("ready", await read);
        Assert.Equal("ready", cache.Get("key"));
        connection.IsConnected.Returns(false);
        Assert.Throws<InvalidOperationException>(() => cache.Get("key"));
        Assert.Throws<InvalidOperationException>(() => cache.Remove("key"));
    }

    [Fact]
    public async Task Slow_connection_does_not_block_startup_and_waiter_cancellation_is_independent()
    {
        var pending = new TaskCompletionSource<IConnectionMultiplexer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var connection = Substitute.For<IConnectionMultiplexer>();
        var service = new RedisConnectionStartup(() => { Interlocked.Increment(ref calls); return pending.Task; }, NullLogger<RedisConnectionStartup>.Instance);
        await service.StartAsync(default).WaitAsync(TimeSpan.FromSeconds(1));
        using var cancelled = new CancellationTokenSource();
        var first = service.GetAsync(cancelled.Token);
        var second = service.GetAsync(default);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(second.IsCompleted);
        pending.SetResult(connection);
        Assert.Same(connection, await second);
        Assert.Equal(1, calls);
        await service.DisposeAsync(); await service.DisposeAsync();
        await connection.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Failed_connect_can_retry_and_shutdown_disposes_a_late_connection_once()
    {
        var pending = new TaskCompletionSource<IConnectionMultiplexer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var connection = Substitute.For<IConnectionMultiplexer>();
        var service = new RedisConnectionStartup(() => Interlocked.Increment(ref calls) == 1
            ? Task.FromException<IConnectionMultiplexer>(new IOException("fixture")) : pending.Task, NullLogger<RedisConnectionStartup>.Instance);
        await Assert.ThrowsAsync<IOException>(() => service.GetAsync(default));
        var retried = service.GetAsync(default);
        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        pending.SetResult(connection);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => retried);
        await connection.Received(1).DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.GetAsync(default));
    }
}
