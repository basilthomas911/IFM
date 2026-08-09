using FluentAssertions;
using Xunit;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public sealed class NatsConsumerOptionsTests
{
    [Fact]
    public void CoreDefaults_PreserveExistingBoundedCapacities()
    {
        var options = new NatsConsumerOptions();

        options.Validate();

        options.DispatcherCount.Should().Be(4);
        options.DispatcherCapacity.Should().Be(4096);
        options.GetSubscriptionCapacity().Should().Be(16384);
    }

    [Fact]
    public void JetStreamDefaults_PreserveExistingOutstandingCapacities()
    {
        var options = new NatsJetStreamConsumerOptions();

        options.Validate();

        options.DispatcherCount.Should().Be(4);
        options.DispatcherCapacity.Should().Be(4096);
        options.GetOutstandingLimit().Should().Be(16384);
        options.GetMaxMessages().Should().Be(16384);
        options.GetThresholdMessages().Should().Be(4096);
    }

    [Fact]
    public void JetStreamThreshold_CannotExceedMaxMessages()
    {
        var options = new NatsJetStreamConsumerOptions
        {
            MaxAckPending = 100,
            MaxMessages = 50,
            ThresholdMessages = 51
        };

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ThresholdMessages*MaxMessages*");
    }
}
