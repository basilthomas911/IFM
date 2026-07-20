using System;
using Xunit;
using FluentAssertions;
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
}
