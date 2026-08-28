using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
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

        Action action = () => options.Validate();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ThresholdMessages*MaxMessages*");
    }

    [Fact]
    public void Enforce_BlocksRequiredNonDurableCoreTraffic()
    {
        var admission = CreateEnforcedAdmissionOptions();
        var options = new NatsConsumerOptions
        {
            FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
            {
                [ActorType.Command] = CoreNatsTrafficClass.RequiredNonDurable,
                [ActorType.Query] = CoreNatsTrafficClass.RequestReplyOnly,
                [ActorType.Function] = CoreNatsTrafficClass.RequestReplyOnly
            }
        };

        Action action = () => options.Validate(admission);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Command*required non-durable*");
    }

    [Fact]
    public void Enforce_AcceptsExplicitRecoverableCoreTraffic()
    {
        var admission = CreateEnforcedAdmissionOptions();
        var options = new NatsConsumerOptions
        {
            FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
            {
                [ActorType.Command] = CoreNatsTrafficClass.RequestReplyOnly,
                [ActorType.Query] = CoreNatsTrafficClass.RequestReplyOnly,
                [ActorType.Function] = CoreNatsTrafficClass.RequestReplyOnly
            }
        };

        options.Invoking(value => value.Validate(admission)).Should().NotThrow();
    }

    [Theory]
    [InlineData(ActorType.Command)]
    [InlineData(ActorType.Query)]
    [InlineData(ActorType.Function)]
    public void Enforce_RequiresRequestReplyOnlyForRequestReplyActors(ActorType actorType)
    {
        var admission = CreateEnforcedAdmissionOptions();
        var options = new NatsConsumerOptions
        {
            FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
            {
                [ActorType.Command] = CoreNatsTrafficClass.RequestReplyOnly,
                [ActorType.Query] = CoreNatsTrafficClass.RequestReplyOnly,
                [ActorType.Function] = CoreNatsTrafficClass.RequestReplyOnly
            }
        };
        options.FireAndForgetTraffic[actorType] = CoreNatsTrafficClass.Optional;

        Action action = () => options.Validate(admission);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{actorType}*RequestReplyOnly*");
    }

    [Fact]
    public void Enforce_RequiresOwnedJetStreamFanoutPayloads()
    {
        var options = new NatsJetStreamConsumerOptions
        {
            UseOwnedEventPayloads = false
        };

        Action action = () => options.Validate(CreateEnforcedAdmissionOptions());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*owned JetStream event payloads*");
    }

    [Theory]
    [InlineData(ActorType.Unknown, CoreNatsTrafficClass.Unknown)]
    [InlineData(ActorType.Command, CoreNatsTrafficClass.Optional)]
    public async Task EnforcedConsumer_BlocksIncompatibleManuallyStartedTrafficType(
        ActorType actorType,
        CoreNatsTrafficClass trafficClass)
    {
        var classifications = new Dictionary<ActorType, CoreNatsTrafficClass>
        {
            [ActorType.Command] = CoreNatsTrafficClass.RequestReplyOnly,
            [ActorType.Query] = CoreNatsTrafficClass.RequestReplyOnly,
            [ActorType.Function] = CoreNatsTrafficClass.RequestReplyOnly
        };
        if (trafficClass != CoreNatsTrafficClass.Unknown)
            classifications[actorType] = trafficClass;
        var consumer = new NatsActorConsumer(
            new NatsConsumerOptions
            {
                FireAndForgetTraffic = classifications
            },
            NullLogger.Instance,
            admissionOptions: CreateEnforcedAdmissionOptions());

        Func<Task> action = () => consumer
            .StartAsync(Substitute.For<IActorSupervisor>(), actorType, "blocked")
            .AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{actorType}*{trafficClass}*");
    }

    static ActorAdmissionOptions CreateEnforcedAdmissionOptions()
    {
        var options = new ActorAdmissionOptions
        {
            Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = 100,
            GlobalByteLimit = 100_000,
            MaximumPayloadBytes = 1_000,
            DefaultActorTypeMessageLimit = 100,
            DefaultActorTypeByteLimit = 100_000,
            DefaultMailboxMessageLimit = 10,
            JetStreamNakDelayMilliseconds = 25,
            OverloadErrorCode = -429
        };
        options.Validate();
        return options;
    }
}
