using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class OptionTradeModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly OptionTradeModel _sut;

    public OptionTradeModelTests()
    {
        _sut = new OptionTradeModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var expected = new OptionTradeReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<OptionTradeReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsDefault()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var data = new OptionTradeReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(entityId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }

    [Fact]
    public void Remove_CallsCacheRemove()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");

        // Act
        _sut.Remove(entityId);

        // Assert
        _redisCache.Received(1).Remove("key");
    }
}

public class TradePlanForwardLossLimitModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly TradePlanForwardLossLimitModel _sut;

    public TradePlanForwardLossLimitModelTests()
    {
        _sut = new TradePlanForwardLossLimitModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        var expected = new Shared.Trade.ViewModels.TradePlanForwardLossLimitReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Shared.Trade.ViewModels.TradePlanForwardLossLimitReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        var data = new Shared.Trade.ViewModels.TradePlanForwardLossLimitReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(entityId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }

    [Fact]
    public void Remove_CallsCacheRemove()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");

        // Act
        _sut.Remove(entityId);

        // Assert
        _redisCache.Received(1).Remove("key");
    }
}

public class FuturesOptionQuoteDataModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesOptionQuoteDataModel _sut;

    public FuturesOptionQuoteDataModelTests()
    {
        _sut = new FuturesOptionQuoteDataModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var quoteId = new FuturesOptionQuoteId(1, "ESZ4_C5000", 100);
        var expected = new FuturesOptionQuoteDataReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<FuturesOptionQuoteDataReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(quoteId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var quoteId = new FuturesOptionQuoteId(1, "ESZ4_C5000", 100);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(quoteId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var quoteId = new FuturesOptionQuoteId(1, "ESZ4_C5000", 100);
        var data = new FuturesOptionQuoteDataReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(quoteId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }

    [Fact]
    public void Clear_SetsEmptyStringInCache()
    {
        // Arrange
        var quoteId = new FuturesOptionQuoteId(1, "ESZ4_C5000", 100);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");

        // Act
        _sut.Clear(quoteId);

        // Assert
        _redisCache.Received(1).Set("key", string.Empty);
    }

    [Fact]
    public void BddConstructor_CreatesInstance()
    {
        // Arrange & Act
        var model = new FuturesOptionQuoteDataModel();

        // Assert
        model.Should().NotBeNull();
    }
}
