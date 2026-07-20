using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class StreamingRequestIdModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly StreamingRequestIdModel _sut;

    public StreamingRequestIdModelTests()
    {
        _sut = new StreamingRequestIdModel(_redisCache, _jsonSerializer);
    }

    private static StreamingRequestId CreateStreamingRequestId()
    {
        var optionContract = new FuturesOptionContractReadModel("ES20241220C5000", "ES Dec 2024 C5000", "ES", "ESZ4_C5000", "OPT", "USD", "CME", "50", new DateOnly(2024, 12, 20), 5000, "Call");
        var underlyingContract = new FuturesContractV2ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        return new StreamingRequestId
        {
            RequestId = 100,
            OptionContract = optionContract,
            UnderlyingContract = underlyingContract,
            ValueDate = new DateOnly(2024, 12, 1)
        };
    }

    [Fact]
    public void GetByString_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var expected = CreateStreamingRequestId();
        _redisCache.Get(Arg.Any<string>()).Returns("{}");
        _jsonSerializer.Deserialize<StreamingRequestId>("{}").Returns(expected);

        // Act
        var result = _sut.Get("ESZ4_C5000");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetByString_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns((string?)null);

        // Act
        var result = _sut.Get("ESZ4_C5000");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetByInt_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var expected = CreateStreamingRequestId();
        _redisCache.Get(Arg.Any<string>()).Returns("{}");
        _jsonSerializer.Deserialize<StreamingRequestId>("{}").Returns(expected);

        // Act
        var result = _sut.Get(100);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetByInt_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns((string?)null);

        // Act
        var result = _sut.Get(100);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_CachesBothContractIdAndRequestIdKeys()
    {
        // Arrange
        var requestId = CreateStreamingRequestId();
        _jsonSerializer.Serialize(requestId).Returns("serialized");

        // Act
        _sut.Set(requestId);

        // Assert
        _redisCache.Received(1).Set(Arg.Is<string>(k => k.Contains("ES20241220C5000")), "serialized", TimeSpan.FromDays(1));
        _redisCache.Received(1).Set(Arg.Is<string>(k => k.Contains("100")), "serialized", TimeSpan.FromDays(1));
    }

    [Fact]
    public void Remove_RemovesBothKeys()
    {
        // Arrange
        var requestId = CreateStreamingRequestId();

        // Act
        _sut.Remove(requestId);

        // Assert
        _redisCache.Received(1).Remove(Arg.Is<string>(k => k.Contains("ES20241220C5000")));
        _redisCache.Received(1).Remove(Arg.Is<string>(k => k.Contains("100")));
    }
}

public class FuturesTickDataStreamingParameterModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly FuturesTickDataStreamingParameterModel _sut;

    public FuturesTickDataStreamingParameterModelTests()
    {
        _sut = new FuturesTickDataStreamingParameterModel(_redisCache);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var contract = new FuturesContractV2ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        var expected = new FuturesTickDataStreamingParameter(100, new DateOnly(2024, 12, 1), contract);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(expected, Newtonsoft.Json.Formatting.None);
        _redisCache.Get(Arg.Any<string>()).Returns(json);

        // Act
        var result = _sut.Get(123);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns((string?)null);

        // Act
        var result = _sut.Get(123);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsDefault()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns(string.Empty);

        // Act
        var result = _sut.Get(123);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var contract = new FuturesContractV2ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        var data = new FuturesTickDataStreamingParameter(100, new DateOnly(2024, 12, 1), contract);

        // Act
        _sut.Set(123, data);

        // Assert
        _redisCache.Received(1).Set(Arg.Any<string>(), Arg.Any<string>());
    }
}

public class VixFuturesContractIdModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly VixFuturesContractIdModel _sut;

    public VixFuturesContractIdModelTests()
    {
        _sut = new VixFuturesContractIdModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsRawString()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("VXZ4");

        // Act
        var result = _sut.Get(new DateOnly(2024, 12, 1));

        // Assert
        result.Should().Be("VXZ4");
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsNull()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsNull()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);

        // Act
        var result = _sut.Get(new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_CachesRawStringValue()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");

        // Act
        _sut.Set(new DateOnly(2024, 12, 1), "VXZ4");

        // Assert
        _redisCache.Received(1).Set("key", "VXZ4");
    }
}

public class SignalProcessorModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly SignalProcessorModel _sut;

    public SignalProcessorModelTests()
    {
        _sut = new SignalProcessorModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedGenericValue()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var expected = new SignalProcessor<double>();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<SignalProcessor<double>>("{}").Returns(expected);

        // Act
        var result = _sut.Get<double>(entityId);

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
        var result = _sut.Get<double>(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesGenericValue()
    {
        // Arrange
        var entityId = new OptionTradeEntityId(1, 2);
        var data = new SignalProcessor<double>();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(entityId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class ForwardLossRatioMapModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly ForwardLossRatioMapModel _sut;

    public ForwardLossRatioMapModelTests()
    {
        _sut = new ForwardLossRatioMapModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedDictionary()
    {
        // Arrange
        var expected = new Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>
        {
            [new DateOnly(2024, 12, 1)] = new List<TradePlanForwardLossRatioReadModel> { new() }
        };
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>>("{}").Returns(expected);

        // Act
        var result = _sut.Get(new DateOnly(2024, 12, 1));

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsEmptyDictionary()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Get_WhenDeserializationReturnsNull_ReturnsEmptyDictionary()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>>("{}").Returns((Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>?)null);

        // Act
        var result = _sut.Get(new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var data = new Dictionary<DateOnly, ICollection<TradePlanForwardLossRatioReadModel>>();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(new DateOnly(2024, 12, 1), data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class FuturesContractModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesContractModel _sut;

    public FuturesContractModelTests()
    {
        _sut = new FuturesContractModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var contractId = new Shared.MarketData.FuturesContractId("ESZ4", "ES", new DateOnly(2024, 12, 20));
        var expected = new FuturesContractV2ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<FuturesContractV2ReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(contractId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var contractId = new Shared.MarketData.FuturesContractId("ESZ4", "ES", new DateOnly(2024, 12, 20));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(contractId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var contractId = new Shared.MarketData.FuturesContractId("ESZ4", "ES", new DateOnly(2024, 12, 20));
        var data = new FuturesContractV2ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(contractId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}
