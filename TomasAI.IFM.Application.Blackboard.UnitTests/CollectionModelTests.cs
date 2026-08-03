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
    private readonly VixFuturesEodDataModel _sut;

    public VixFuturesEodDataModelTests()
    {
        _sut = new VixFuturesEodDataModel(_redisCache, _jsonSerializer);
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
    private readonly ReferenceLookupModel _sut;

    public ReferenceLookupModelTests()
    {
        _sut = new ReferenceLookupModel(_redisCache, _jsonSerializer);
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
    private readonly DomainEventsModel _sut;

    public DomainEventsModelTests()
    {
        _sut = new DomainEventsModel(_redisCache, _jsonSerializer);
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

public class FuturesOptionQuoteModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesOptionQuoteModel _sut;

    public FuturesOptionQuoteModelTests()
    {
        _sut = new FuturesOptionQuoteModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDictionary()
    {
        // Arrange
        var quotes = new[]
        {
            new FuturesOptionQuoteReadModel(1, "ESZ4_C5000", 100, "test", DateTime.UtcNow),
            new FuturesOptionQuoteReadModel(1, "ESZ4_P5000", 101, "test", DateTime.UtcNow)
        };
        const string key = "FuturesOptionQuote:1";
        _redisCache.Get(key).Returns("[{},{}]");
        _jsonSerializer.Deserialize<FuturesOptionQuoteReadModel[]>("[{},{}]").Returns(quotes);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey(100);
        result.Should().ContainKey(101);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsEmptyDictionary()
    {
        // Arrange
        const string key = "FuturesOptionQuote:1";
        _redisCache.Get(key).Returns((string?)null);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsEmptyDictionary()
    {
        // Arrange
        const string key = "FuturesOptionQuote:1";
        _redisCache.Get(key).Returns(string.Empty);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Get_WhenDeserializationReturnsNull_ReturnsEmptyDictionary()
    {
        // Arrange
        const string key = "FuturesOptionQuote:1";
        _redisCache.Get(key).Returns("[]");
        _jsonSerializer.Deserialize<FuturesOptionQuoteReadModel[]>("[]").Returns((FuturesOptionQuoteReadModel[]?)null);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var quotes = new[] { new FuturesOptionQuoteReadModel() };
        const string key = "FuturesOptionQuote:1";
        _jsonSerializer.Serialize(quotes).Returns("value");

        // Act
        _sut.Set(1, quotes);

        // Assert
        _redisCache.Received(1).Set(key, "value");
    }

    [Fact]
    public void Clear_SetsEmptyStringInCache()
    {
        // Arrange
        const string key = "FuturesOptionQuote:1";

        // Act
        _sut.Clear(1);

        // Assert
        _redisCache.Received(1).Set(key, string.Empty);
    }
}
