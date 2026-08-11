using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class VixFuturesEodDataModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly VixFuturesEodDataCacheModel _sut;

    public VixFuturesEodDataModelTests()
    {
        _sut = new VixFuturesEodDataCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedCollection()
    {
        // Arrange
        var expected = new List<VixFuturesEodDataReadModel> { new() };
        const string key = "VixFuturesEodData:VXZ4-20241201";
        _redisCache.Get(key).Returns("[{}]");
        _jsonSerializer.Deserialize<ICollection<VixFuturesEodDataReadModel>>("[{}]").Returns(expected);

        // Act
        var result = _sut.Get("VXZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsEmptyCollection()
    {
        // Arrange
        const string key = "VixFuturesEodData:VXZ4-20241201";
        _redisCache.Get(key).Returns((string?)null);

        // Act
        var result = _sut.Get("VXZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsEmptyCollection()
    {
        // Arrange
        const string key = "VixFuturesEodData:VXZ4-20241201";
        _redisCache.Get(key).Returns(string.Empty);

        // Act
        var result = _sut.Get("VXZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Get_WhenDeserializationReturnsNull_ReturnsEmptyCollection()
    {
        // Arrange
        const string key = "VixFuturesEodData:VXZ4-20241201";
        _redisCache.Get(key).Returns("[{}]");
        _jsonSerializer.Deserialize<ICollection<VixFuturesEodDataReadModel>>("[{}]").Returns((ICollection<VixFuturesEodDataReadModel>?)null);

        // Act
        var result = _sut.Get("VXZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        ICollection<VixFuturesEodDataReadModel> data = [new()];
        const string key = "VixFuturesEodData:VXZ4-20241201";
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set("VXZ4", new DateOnly(2024, 12, 1), data);

        // Assert
        _redisCache.Received(1).Set(key, "value");
    }
}
public class ReferenceLookupModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly ReferenceLookupCacheModel _sut;

    public ReferenceLookupModelTests()
    {
        _sut = new ReferenceLookupCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedCollection()
    {
        // Arrange
        var expected = new Dictionary<string, List<string>> { { "key1", new List<string> { "value1" } } };
        const string key = "ReferenceLookup";
        _redisCache.Get(key).Returns("[{}]");
        _jsonSerializer.Deserialize<Dictionary<string, List<string>>>("[{}]").Returns(expected);

        // Act
        var result = _sut.Get();

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsNull()
    {
        // Arrange
        const string key = "ReferenceLookup";
        _redisCache.Get(key).Returns((string?)null);

        // Act
        var result = _sut.Get();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var data = new Dictionary<string, List<string>> { { "key1", new List<string> { "value1" } } };
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(data);

        // Assert
        _redisCache.Received(1).Set("ReferenceLookup", "value");
    }
}

public class DomainEventsModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly DomainEventsCacheModel _sut;

    public DomainEventsModelTests()
    {
        _sut = new DomainEventsCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedCollection()
    {
        // Arrange
        var commandId = Guid.NewGuid();
        var key = $"DomainEvents:{commandId}";
        var expected = new DomainEventCollection();
        _redisCache.Get(key).Returns("[]");
        _jsonSerializer.Deserialize<DomainEventCollection>("[]").Returns(expected);

        // Act
        var result = _sut.Get(commandId);

        // Assert
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsEmptyCollection()
    {
        // Arrange
        var commandId = Guid.NewGuid();
        var key = $"DomainEvents:{commandId}";
        _redisCache.Get(key).Returns((string?)null);

        // Act
        var result = _sut.Get(commandId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsEmptyCollection()
    {
        // Arrange
        var commandId = Guid.NewGuid();
        var key = $"DomainEvents:{commandId}";
        _redisCache.Get(key).Returns(string.Empty);

        // Act
        var result = _sut.Get(commandId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var commandId = Guid.NewGuid();
        var key = $"DomainEvents:{commandId}";
        var data = new DomainEventCollection();
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(commandId, data);

        // Assert
        _redisCache.Received(1).Set(key, "value");
    }
}
// Legacy quote-cache collection tests were removed.
