namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class ExplicitBrokerUrlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("nats://127.0.0.1:4222")]
    [InlineData("nats://remote:45222")]
    [InlineData("nats://user:secret@127.0.0.1:45222")]
    [InlineData("http://127.0.0.1:45222")]
    [InlineData("nats://127.0.0.1:45222/foreign")]
    public void Missing_or_implicit_development_broker_is_rejected(string? value)
        => Assert.Throws<InvalidOperationException>(() => Harness.ExplicitBrokerUrl.Validate(value));

    [Fact]
    public void Explicit_isolated_loopback_broker_is_preserved()
        => Assert.Equal("nats://127.0.0.1:45222", Harness.ExplicitBrokerUrl.Validate("nats://127.0.0.1:45222"));
}
