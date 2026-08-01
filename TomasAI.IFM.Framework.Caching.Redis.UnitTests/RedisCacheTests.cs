using System;
using Xunit;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using StackExchange.Redis;


namespace TomasAI.IFM.Framework.Caching.Redis.UnitTests;

/// <summary>
/// Provides unit tests for the <see cref="RedisCache"/> class, verifying its behavior when interacting with a Redis
/// cache.
/// </summary>
/// <remarks>This test class includes methods to validate the functionality of the <see cref="RedisCache"/> class,
/// such as retrieving,  setting, and removing cache entries. The tests use a local Redis instance running on
/// "localhost:6379".</remarks>
public class RedisCacheTests
{
    private static readonly DateTimeOffset TestUtcNow =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Set_WithAbsoluteExpiryAndShorterTtl_UsesTtl()
    {
        var (redisCache, database) = CreateMockedCache(TestUtcNow);
        var ttl = TimeSpan.FromMinutes(15);

        redisCache.Set("key", "value", TestUtcNow.AddHours(24), ttl);

        database.Received(1).StringSet(
            (RedisKey)"key",
            (RedisValue)"value",
            Arg.Is<Expiration>(expiration => expiration.Equals(new Expiration(ttl))),
            ValueCondition.Always);
    }

    [Fact]
    public void Set_WithAbsoluteExpirySoonerThanTtl_UsesExactAbsoluteExpiry()
    {
        var (redisCache, database) = CreateMockedCache(TestUtcNow);
        var absoluteExpiry = TestUtcNow.AddMinutes(5);

        redisCache.Set("key", "value", absoluteExpiry, TimeSpan.FromMinutes(15));

        database.Received(1).StringSet(
            (RedisKey)"key",
            (RedisValue)"value",
            Arg.Is<Expiration>(expiration => expiration.Equals(
                new Expiration(absoluteExpiry.UtcDateTime))),
            ValueCondition.Always);
    }

    [Fact]
    public void Set_WithExpiredAbsoluteExpiry_Throws()
    {
        var (redisCache, database) = CreateMockedCache(TestUtcNow);

        var act = () => redisCache.Set(
            "key",
            "value",
            TestUtcNow,
            TimeSpan.FromMinutes(15));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("absoluteExpiry");
        database.DidNotReceiveWithAnyArgs().StringSet(
            default,
            default,
            default(Expiration),
            default!);
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpiryAndTtl_UsesEarlierDeadline()
    {
        var (redisCache, database) = CreateMockedCache(TestUtcNow);
        var ttl = TimeSpan.FromMinutes(15);
        database.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<Expiration>(),
                Arg.Any<ValueCondition>())
            .Returns(Task.FromResult(true));

        await redisCache.SetAsync("key", "value", TestUtcNow.AddHours(24), ttl);

        await database.Received(1).StringSetAsync(
            (RedisKey)"key",
            (RedisValue)"value",
            Arg.Is<Expiration>(expiration => expiration.Equals(new Expiration(ttl))),
            ValueCondition.Always);
    }

    [Fact]
    public void RemoveByPrefix_ScansAndDeletesOnlyReturnedKeys()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        var server = Substitute.For<IServer>();
        var endpoint = new DnsEndPoint("localhost", 6379);
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        multiplexer.GetEndPoints(false).Returns([endpoint]);
        multiplexer.GetServer(endpoint, Arg.Any<object>()).Returns(server);
        database.Database.Returns(0);
        server.IsConnected.Returns(true);
        server.IsReplica.Returns(false);
        server.ServerType.Returns(ServerType.Standalone);
        server.Keys(
                Arg.Any<int>(),
                Arg.Any<RedisValue>(),
                Arg.Any<int>(),
                Arg.Any<long>(),
                Arg.Any<int>(),
                Arg.Any<CommandFlags>())
            .Returns([(RedisKey)"Databento:one", (RedisKey)"Databento:two"]);
        database.KeyDelete(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(true);
        var redisCache = new RedisCache(multiplexer);

        var removed = redisCache.RemoveByPrefix("Data[bento]*?");

        removed.Should().Be(2);
        server.Received(1).Keys(
            0,
            Arg.Is<RedisValue>(pattern => pattern.ToString() == "Data\\[bento\\]\\*\\?*"),
            250,
            Arg.Any<long>(),
            Arg.Any<int>(),
            Arg.Any<CommandFlags>());
        database.Received(1).KeyDelete(
            (RedisKey)"Databento:one",
            Arg.Any<CommandFlags>());
        database.Received(1).KeyDelete(
            (RedisKey)"Databento:two",
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void GetOk()
    {
        IConnectionMultiplexer redisMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var redisCache = new RedisCache(redisMultiplexer);
        redisCache.Set("testKey", "abc123");
        var testValue = redisCache.Get("testKey");
        testValue.Should().Be("abc123");
    }

    [Fact]
    public void GetWithNonExistingKey()
    {
        IConnectionMultiplexer redisMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var redisCache = new RedisCache(redisMultiplexer);
        var testValue = redisCache.Get($"{Guid.NewGuid()}");
        testValue.Should().BeNullOrEmpty();
    }

    [Fact]
    public void RemoveOk()
    {
        IConnectionMultiplexer redisMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var redisCache = new RedisCache(redisMultiplexer);
        redisCache.Set("testKey", "abc123");
        var testValue = redisCache.Get("testKey");
        testValue.Should().Be("abc123");
        redisCache.Remove("testKey");
        testValue = redisCache.Get("testKey");
        testValue.Should().BeNullOrEmpty();
    }

    [Fact]
    public void DeleteAllKeys_RemovesAllKeysFromDatabase()
    {
        IConnectionMultiplexer redisMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var redisCache = new RedisCache(redisMultiplexer);

        // Arrange: Set multiple keys
        redisCache.Set("key1", "value1");
        redisCache.Set("key2", "value2");
        redisCache.Set("key3", "value3");

        redisCache.Get("key1").Should().Be("value1");
        redisCache.Get("key2").Should().Be("value2");
        redisCache.Get("key3").Should().Be("value3");

        // Act: Delete all keys
        redisCache.DeleteAllKeys();

        // Assert: All keys should be removed
        redisCache.Get("key1").Should().BeNullOrEmpty();
        redisCache.Get("key2").Should().BeNullOrEmpty();
        redisCache.Get("key3").Should().BeNullOrEmpty();
    }

    private static (RedisCache Cache, IDatabase Database) CreateMockedCache(
        DateTimeOffset utcNow)
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        return (new RedisCache(multiplexer, new ManualTimeProvider(utcNow)), database);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
