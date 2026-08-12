using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJetStreamEventListenerTests
{
    [Fact]
    public void JetStream_contract_inherits_established_listener_contract()
    {
        typeof(IActorEventListener).IsAssignableFrom(typeof(IJSActorEventListener)).Should().BeTrue();
    }

    [Fact]
    public async Task Registrations_resolve_Core_and_JetStream_listeners_independently()
    {
        var services = new ServiceCollection();
        services.AddNatsActorEventListeners();
        await using var provider = services.BuildServiceProvider();

        var core = provider.GetRequiredService<IActorEventListener>();
        var jetStream = provider.GetRequiredService<IJSActorEventListener>();

        core.Should().BeOfType<NatsActorEventListener>();
        jetStream.Should().BeOfType<NatsJetStreamEventListener>();
        jetStream.Should().NotBeSameAs(core);
    }

    [Fact]
    public void Options_derive_ack_window_from_bounded_admission_capacity()
    {
        var options = new NatsJetStreamEventListenerOptions
        {
            DispatcherCount = 3,
            DispatcherCapacity = 20
        };

        options.GetOutstandingLimit().Should().Be(60);
        options.GetMaxMessages().Should().Be(60);
        options.GetThresholdMessages().Should().Be(20);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Options_reject_non_positive_dispatcher_bounds(int dispatcherCount, int dispatcherCapacity)
    {
        var options = new NatsJetStreamEventListenerOptions
        {
            DispatcherCount = dispatcherCount,
            DispatcherCapacity = dispatcherCapacity
        };

        var act = options.Validate;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Durable_name_is_stable_valid_and_mailbox_specific()
    {
        var mailbox = new ActorMailboxId(ActorType.Event, "DatabaseBackup");

        var first = NatsJetStreamEventListener.CreateDurableConsumerName(
            "ifm-backup",
            "local workstation listener",
            mailbox);
        var second = NatsJetStreamEventListener.CreateDurableConsumerName(
            "ifm-backup",
            "local workstation listener",
            mailbox);
        var other = NatsJetStreamEventListener.CreateDurableConsumerName(
            "ifm-backup",
            "local workstation listener",
            new ActorMailboxId(ActorType.Event, "Other"));

        first.Should().Be(second);
        first.Should().NotBe(other);
        first.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        first.Length.Should().BeLessThanOrEqualTo(120);
    }

    [Fact]
    public async Task Start_rejects_empty_event_map_before_connecting()
    {
        var listener = new NatsJetStreamEventListener(
            new NatsJetStreamEventListenerOptions(),
            Substitute.For<ILogger>());

        var act = async () => await listener.StartAsync(
            "listener",
            [],
            static (_, _) => ValueTask.CompletedTask);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Event map cannot be empty*");
    }
}
