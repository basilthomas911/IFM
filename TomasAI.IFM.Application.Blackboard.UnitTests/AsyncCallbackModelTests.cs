using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

#region EventNameIdModel Tests

public class EventNameIdModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly EventNameIdModel _sut;

    public EventNameIdModelTests()
    {
        _sut = new EventNameIdModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var eventName = "TestEvent";
        var eventTypeName = "TestType";
        var cachedJson = "{\"json\"}";
        var expected = new EventNameIdReadModel(1, eventName, eventTypeName);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.TryGet("key", out Arg.Any<string?>())
            .Returns(x => { x[1] = cachedJson; return true; });
        _jsonSerializer.Deserialize<EventNameIdReadModel>(cachedJson).Returns(expected);
        var callback = Substitute.For<Func<string, string, Task<EventNameIdReadModel>>>();

        // Act
        var result = await _sut.GetAsync(eventName, eventTypeName, callback);

        // Assert
        result.Should().Be(expected);
        await callback.DidNotReceive().Invoke(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var eventName = "TestEvent";
        var eventTypeName = "TestType";
        var callbackResult = new EventNameIdReadModel(5, eventName, eventTypeName);
        var serializedResult = "{\"serialized\"}";
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.TryGet("key", out Arg.Any<string?>())
            .Returns(x => { x[1] = null; return false; });
        var callback = Substitute.For<Func<string, string, Task<EventNameIdReadModel>>>();
        callback(eventName, eventTypeName).Returns(Task.FromResult(callbackResult));
        _jsonSerializer.Serialize(callbackResult).Returns(serializedResult);
        _jsonSerializer.Deserialize<EventNameIdReadModel>(serializedResult).Returns(callbackResult);

        // Act
        var result = await _sut.GetAsync(eventName, eventTypeName, callback);

        // Assert
        result.Should().Be(callbackResult);
        _redisCache.Received(1).Set("key", serializedResult);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_EmptySerializedValue_ReturnsDefault()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.TryGet("key", out Arg.Any<string?>())
            .Returns(x => { x[1] = null; return false; });
        var callback = Substitute.For<Func<string, string, Task<EventNameIdReadModel>>>();
        callback(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(default(EventNameIdReadModel)));
        _jsonSerializer.Serialize(Arg.Any<EventNameIdReadModel>()).Returns(string.Empty);

        // Act
        var result = await _sut.GetAsync("e", "t", callback);

        // Assert
        result.EventNameId.Should().Be(-1);
        result.EventName.Should().BeEmpty();
        result.EventTypeName.Should().BeEmpty();
    }

    [Fact]
    public void BddConstructor_DoesNotThrow()
    {
        // Act
        var model = new EventNameIdModel();

        // Assert
        model.Should().NotBeNull();
    }
}

#endregion

#region EventStreamIdModel Tests

public class EventStreamIdModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly EventStreamIdModel _sut;

    public EventStreamIdModelTests()
    {
        _sut = new EventStreamIdModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var eventStream = "TestStream";
        var cachedJson = "{\"json\"}";
        var expected = new EventStreamIdReadModel(42, eventStream);
        _redisCache.TryGet(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x => { x[1] = cachedJson; return true; });
        _jsonSerializer.Deserialize<EventStreamIdReadModel>(cachedJson).Returns(expected);
        var callback = Substitute.For<Func<string, Task<EventStreamIdReadModel>>>();

        // Act
        var result = await _sut.GetAsync(eventStream, callback);

        // Assert
        result.Should().Be(expected);
        await callback.DidNotReceive().Invoke(Arg.Any<string>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var eventStream = "TestStream";
        var callbackResult = new EventStreamIdReadModel(10, eventStream);
        var serializedResult = "{\"serialized\"}";
        _redisCache.TryGet(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x => { x[1] = null; return false; });
        var callback = Substitute.For<Func<string, Task<EventStreamIdReadModel>>>();
        callback(eventStream).Returns(Task.FromResult(callbackResult));
        _jsonSerializer.Serialize(callbackResult).Returns(serializedResult);
        _jsonSerializer.Deserialize<EventStreamIdReadModel>(serializedResult).Returns(callbackResult);

        // Act
        var result = await _sut.GetAsync(eventStream, callback);

        // Assert
        result.Should().Be(callbackResult);
        _redisCache.Received(1).Set(Arg.Any<string>(), serializedResult);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_NullDeserialization_ReturnsDefault()
    {
        // Arrange
        _redisCache.TryGet(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x => { x[1] = null; return false; });
        var callback = Substitute.For<Func<string, Task<EventStreamIdReadModel>>>();
        callback(Arg.Any<string>()).Returns(Task.FromResult(new EventStreamIdReadModel(0, "")));
        _jsonSerializer.Serialize(Arg.Any<EventStreamIdReadModel>()).Returns("val");
        _jsonSerializer.Deserialize<EventStreamIdReadModel>("val")
            .Returns((EventStreamIdReadModel?)null);

        // Act
        var result = await _sut.GetAsync("stream", callback);

        // Assert
        result.EventStreamId.Should().Be(0);
        result.EventStream.Should().BeEmpty();
    }

    [Fact]
    public void Remove_RemovesCacheEntry()
    {
        // Arrange
        var eventStream = "TestStream";

        // Act
        _sut.Remove(eventStream);

        // Assert
        _redisCache.Received(1).Remove(Arg.Any<string>());
    }

    [Fact]
    public void BddConstructor_DoesNotThrow()
    {
        // Act
        var model = new EventStreamIdModel();

        // Assert
        model.Should().NotBeNull();
    }
}

#endregion

#region FuturesContractSymbolModel Tests

public class FuturesContractSymbolModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesContractSymbolModel _sut;

    public FuturesContractSymbolModelTests()
    {
        _sut = new FuturesContractSymbolModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedSymbol()
    {
        // Arrange
        var contractId = "ESZ4";
        var cachedJson = "{\"symbol\"}";
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(cachedJson);
        _jsonSerializer.Deserialize<string>(cachedJson).Returns("ES");
        var callback = Substitute.For<Func<string, ValueTask<string>>>();

        // Act
        var result = await _sut.GetAsync(contractId, callback);

        // Assert
        result.Should().Be("ES");
        await callback.DidNotReceive().Invoke(Arg.Any<string>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var contractId = "ESZ4";
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, ValueTask<string>>>();
        callback(contractId).Returns(new ValueTask<string>("ES"));
        _jsonSerializer.Serialize("ES").Returns("{\"ES\"}");
        _jsonSerializer.Deserialize<string>("{\"ES\"}").Returns("ES");

        // Act
        var result = await _sut.GetAsync(contractId, callback);

        // Assert
        result.Should().Be("ES");
        _redisCache.Received(1).Set("key", "{\"ES\"}");
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallbackReturnsEmpty_ReturnsEmpty()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, ValueTask<string>>>();
        callback(Arg.Any<string>()).Returns(new ValueTask<string>(string.Empty));

        // Act
        var result = await _sut.GetAsync("ESZ4", callback);

        // Assert
        result.Should().BeEmpty();
        _redisCache.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetAsync_CacheHit_NullDeserialization_ReturnsEmpty()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("cached");
        _jsonSerializer.Deserialize<string>("cached").Returns((string?)null);
        var callback = Substitute.For<Func<string, ValueTask<string>>>();

        // Act
        var result = await _sut.GetAsync("ESZ4", callback);

        // Assert
        result.Should().BeEmpty();
    }
}

#endregion

#region FuturesOpenPriceModel Tests

public class FuturesOpenPriceModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesOpenPriceModel _sut;

    public FuturesOpenPriceModelTests()
    {
        _sut = new FuturesOpenPriceModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDecimalValue()
    {
        // Arrange
        var futuresDataId = new FuturesDataId("ESZ4", new DateOnly(2024, 1, 15));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("5250.50");
        var callback = Substitute.For<Func<FuturesDataId, Task<decimal>>>();

        // Act
        var result = await _sut.GetAsync(futuresDataId, callback);

        // Assert
        result.Should().Be(5250.50m);
        await callback.DidNotReceive().Invoke(Arg.Any<FuturesDataId>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var futuresDataId = new FuturesDataId("ESZ4", new DateOnly(2024, 1, 15));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<FuturesDataId, Task<decimal>>>();
        callback(futuresDataId).Returns(Task.FromResult(5300.75m));

        // Act
        var result = await _sut.GetAsync(futuresDataId, callback);

        // Assert
        result.Should().Be(5300.75m);
        _redisCache.Received(1).Set("key", "5300.75");
    }

    [Fact]
    public void Clear_SetsCacheToEmpty()
    {
        // Arrange
        var futuresDataId = new FuturesDataId("ESZ4", new DateOnly(2024, 1, 15));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");

        // Act
        _sut.Clear(futuresDataId);

        // Assert
        _redisCache.Received(1).Set("key", string.Empty);
    }
}

#endregion

#region VixFuturesOpenPriceModel Tests

public class VixFuturesOpenPriceModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly VixFuturesOpenPriceModel _sut;

    public VixFuturesOpenPriceModelTests()
    {
        _sut = new VixFuturesOpenPriceModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDecimalValue()
    {
        // Arrange
        var entityId = new VixFuturesEodDataEntityId("VXZ4", new DateOnly(2024, 1, 15));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("18.25");
        var callback = Substitute.For<Func<VixFuturesEodDataEntityId, Task<decimal>>>();

        // Act
        var result = await _sut.GetAsync(entityId, callback);

        // Assert
        result.Should().Be(18.25m);
        await callback.DidNotReceive().Invoke(Arg.Any<VixFuturesEodDataEntityId>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var entityId = new VixFuturesEodDataEntityId("VXZ4", new DateOnly(2024, 1, 15));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<VixFuturesEodDataEntityId, Task<decimal>>>();
        callback(entityId).Returns(Task.FromResult(20.50m));

        // Act
        var result = await _sut.GetAsync(entityId, callback);

        // Assert
        result.Should().Be(20.50m);
        _redisCache.Received(1).Set("key", "20.50");
    }

    [Fact]
    public void Clear_SetsCacheToEmpty()
    {
        // Arrange
        var entityId = new VixFuturesEodDataEntityId("VXZ4", new DateOnly(2024, 1, 15));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");

        // Act
        _sut.Clear(entityId);

        // Assert
        _redisCache.Received(1).Set("key", string.Empty);
    }
}

#endregion

#region NormalCurveTableModel Tests

public class NormalCurveTableModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly NormalCurveTableModel _sut;

    public NormalCurveTableModelTests()
    {
        _sut = new NormalCurveTableModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 1, 15);
        var cachedJson = "{\"cached\"}";
        var expected = new NormalCurveTableReadModel();
        _redisCache.Get(Arg.Any<string>()).Returns(cachedJson);
        _jsonSerializer.Deserialize<NormalCurveTableReadModel>(cachedJson).Returns(expected);
        var callback = Substitute.For<Func<ValueTask<NormalCurveTableReadModel>>>();

        // Act
        var result = await _sut.GetAsync(valueDate, callback);

        // Assert
        result.Should().Be(expected);
        await callback.DidNotReceive().Invoke();
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 1, 15);
        var callbackResult = new NormalCurveTableReadModel();
        var serializedResult = "{\"serialized\"}";
        _redisCache.Get(Arg.Any<string>()).Returns(string.Empty);
        var callback = Substitute.For<Func<ValueTask<NormalCurveTableReadModel>>>();
        callback().Returns(new ValueTask<NormalCurveTableReadModel>(callbackResult));
        _jsonSerializer.Serialize(callbackResult).Returns(serializedResult);
        _jsonSerializer.Deserialize<NormalCurveTableReadModel>(serializedResult).Returns(callbackResult);

        // Act
        var result = await _sut.GetAsync(valueDate, callback);

        // Assert
        result.Should().Be(callbackResult);
        _redisCache.Received(1).Set(Arg.Any<string>(), serializedResult);
    }

    [Fact]
    public void Remove_RemovesCacheEntry()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 1, 15);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("removeKey");

        // Act
        _sut.Remove(valueDate);

        // Assert
        _redisCache.Received(1).Remove("removeKey");
    }
}

#endregion

#region RiskFreeRateModel Tests

public class RiskFreeRateModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly RiskFreeRateModel _sut;

    public RiskFreeRateModelTests()
    {
        _sut = new RiskFreeRateModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDoubleValue()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 1, 15);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("0.0525");
        var callback = Substitute.For<Func<DateOnly, ValueTask<double>>>();

        // Act
        var result = await _sut.GetAsync(valueDate, callback);

        // Assert
        result.Should().Be(0.0525);
        await callback.DidNotReceive().Invoke(Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 1, 15);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<DateOnly, ValueTask<double>>>();
        callback(valueDate).Returns(new ValueTask<double>(0.045));

        // Act
        var result = await _sut.GetAsync(valueDate, callback);

        // Assert
        result.Should().Be(0.045);
        _redisCache.Received(1).Set("key", "0.045");
    }

    [Fact]
    public async Task GetAsync_CacheHit_ZeroValue_ReturnsCachedZero()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 6, 1);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("0");
        var callback = Substitute.For<Func<DateOnly, ValueTask<double>>>();

        // Act
        var result = await _sut.GetAsync(valueDate, callback);

        // Assert
        result.Should().Be(0.0);
        await callback.DidNotReceive().Invoke(Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_NullCacheValue_CallsCallback()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 3, 20);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);
        var callback = Substitute.For<Func<DateOnly, ValueTask<double>>>();
        callback(valueDate).Returns(new ValueTask<double>(0.035));

        // Act
        var result = await _sut.GetAsync(valueDate, callback);

        // Assert
        result.Should().Be(0.035);
        _redisCache.Received(1).Set("key", "0.035");
    }

    [Fact]
    public void Clear_SetsCacheToEmpty()
    {
        // Arrange
        var valueDate = new DateOnly(2024, 1, 15);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");

        // Act
        _sut.Clear(valueDate);

        // Assert
        _redisCache.Received(1).Set("key", string.Empty);
    }
}

#endregion
