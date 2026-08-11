using TomasAI.IFM.Domain.Trade.Shared;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class OptionTradeModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly OptionTradeCacheModel _sut;

    public OptionTradeModelTests()
    {
        _sut = new OptionTradeCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var expectedKey = "OptionTrade:1.2";
        var cachedJson = "{}";
        var expected = new OptionTradeReadModel();
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<OptionTradeReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<OptionTradeReadModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var expectedKey = "OptionTrade:1.2";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<OptionTradeReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsDefault()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var expectedKey = "OptionTrade:1.2";
        _redisCache.Get(expectedKey).Returns(string.Empty);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<OptionTradeReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var data = new OptionTradeReadModel();
        var expectedKey = "OptionTrade:1.2";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(entityId, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }

    [Fact]
    public void Remove_CallsCacheRemove()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var expectedKey = "OptionTrade:1.2";

        // Act
        _sut.Remove(entityId);

        // Assert
        _redisCache.Received(1).Remove(expectedKey);
    }
}
public class TradePlanForwardLossLimitModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly TradePlanForwardLossLimitCacheModel _sut;

    public TradePlanForwardLossLimitModelTests()
    {
        _sut = new TradePlanForwardLossLimitCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        var expectedKey = "TradePlanForwardLossLimit:1.2.20241201.Unknown";
        var cachedJson = "{}";
        var expected = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanForwardLossLimitReadModel();
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanForwardLossLimitReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanForwardLossLimitReadModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        var expectedKey = "TradePlanForwardLossLimit:1.2.20241201.Unknown";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanForwardLossLimitReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        var data = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanForwardLossLimitReadModel();
        var expectedKey = "TradePlanForwardLossLimit:1.2.20241201.Unknown";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(entityId, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }

    [Fact]
    public void Remove_CallsCacheRemove()
    {
        // Arrange
        var entityId = new TradePlanForwardLossLimitEntityId(1, 2, new DateOnly(2024, 12, 1), TradeType.Unknown);
        var expectedKey = "TradePlanForwardLossLimit:1.2.20241201.Unknown";

        // Act
        _sut.Remove(entityId);

        // Assert
        _redisCache.Received(1).Remove(expectedKey);
    }
}
// Legacy quote-cache get/set tests were removed.
