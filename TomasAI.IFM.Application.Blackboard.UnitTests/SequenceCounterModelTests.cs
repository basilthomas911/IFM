using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class SequenceCounterModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly SequenceCounterModel _sut;

    private enum TestCounter { OrderSequence, TradeSequence }

    public SequenceCounterModelTests()
    {
        _sut = new SequenceCounterModel(_redisCache);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsParsedValue()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns("42");

        // Act
        var result = _sut.Get(TestCounter.OrderSequence);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsZero()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns((string?)null);

        // Act
        var result = _sut.Get(TestCounter.OrderSequence);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsZero()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns(string.Empty);

        // Act
        var result = _sut.Get(TestCounter.OrderSequence);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Get_UsesCorrectCacheKey()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns("1");

        // Act
        _sut.Get(TestCounter.TradeSequence);

        // Assert
        _redisCache.Received(1).Get(Arg.Is<string>(k => k.Contains("SequenceCounter") && k.Contains("TradeSequence")));
    }

    [Fact]
    public void Increment_CallsRedisCacheIncrement()
    {
        // Arrange
        _redisCache.Increment(Arg.Any<string>()).Returns(1L);

        // Act
        var result = _sut.Increment(TestCounter.OrderSequence);

        // Assert
        result.Should().Be(1);
        _redisCache.Received(1).Increment(Arg.Is<string>(k => k.Contains("SequenceCounter") && k.Contains("OrderSequence")));
    }

    [Fact]
    public void Increment_ReturnsIncrementedValue()
    {
        // Arrange
        _redisCache.Increment(Arg.Any<string>()).Returns(5L);

        // Act
        var result = _sut.Increment(TestCounter.TradeSequence);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void Increment_UsesCorrectCacheKey()
    {
        // Arrange
        _redisCache.Increment(Arg.Any<string>()).Returns(1L);

        // Act
        _sut.Increment(TestCounter.TradeSequence);

        // Assert
        _redisCache.Received(1).Increment(Arg.Is<string>(k => k.Contains("SequenceCounter") && k.Contains("TradeSequence")));
    }
}
