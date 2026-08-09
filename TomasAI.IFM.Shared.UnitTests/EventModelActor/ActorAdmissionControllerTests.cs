using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorAdmissionControllerTests
{
    [Fact]
    public void ObserveOnly_AllowsUnsetCapacityLimits()
    {
        var options = new ActorAdmissionOptions { Mode = ActorAdmissionMode.ObserveOnly };

        var action = options.Validate;

        action.Should().NotThrow();
    }

    [Fact]
    public void Enforce_RequiresPositiveCapacityLimits()
    {
        var options = new ActorAdmissionOptions { Mode = ActorAdmissionMode.Enforce };

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires positive global message*");
    }

    [Fact]
    public void ActorTypeLimit_CannotExceedGlobalLimit()
    {
        var options = new ActorAdmissionOptions
        {
            Mode = ActorAdmissionMode.ObserveOnly,
            GlobalMessageLimit = 10,
            DefaultMailboxMessageLimit = 10,
            ActorTypes =
            {
                [ActorType.Command] = new ActorTypeAdmissionOptions { MessageLimit = 11 }
            }
        };

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot exceed its process-wide limit*");
    }

    [Fact]
    public void Enforce_WithCompleteLimits_CreatesController()
    {
        var options = new ActorAdmissionOptions
        {
            Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = 100,
            GlobalByteLimit = 10_000,
            MaximumPayloadBytes = 1_000,
            DefaultActorTypeMessageLimit = 50,
            DefaultActorTypeByteLimit = 5_000,
            DefaultMailboxMessageLimit = 25
        };

        var action = () => new ActorAdmissionController(options);

        action.Should().NotThrow();
    }

    [Fact]
    public void Enforce_AdmitsExactGlobalCapacity_ThenRejectsAndReleasesToZero()
    {
        var controller = new ActorAdmissionController(CreateEnforcedOptions(
            globalMessages: 2,
            globalBytes: 20,
            typeMessages: 2,
            typeBytes: 20));
        var message = new SizedActorMessage(10);

        controller.TryReserve(message, ActorType.Command, out var first).Accepted.Should().BeTrue();
        controller.TryReserve(message, ActorType.Command, out var second).Accepted.Should().BeTrue();
        var rejected = controller.TryReserve(message, ActorType.Command, out _);

        rejected.Should().Be(ActorAdmissionResult.Rejected(ActorAdmissionReason.GlobalMessageLimit));
        controller.CurrentMessageCount.Should().Be(2);
        controller.CurrentByteCount.Should().Be(20);

        controller.Release(second);
        controller.Release(first);
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
    }

    [Fact]
    public void Enforce_RejectsGlobalByteLimit_AndRollsBackMessageReservation()
    {
        var controller = new ActorAdmissionController(CreateEnforcedOptions(
            globalMessages: 3,
            globalBytes: 15,
            typeMessages: 3,
            typeBytes: 15));
        var message = new SizedActorMessage(10);

        controller.TryReserve(message, ActorType.Command, out var accepted).Accepted.Should().BeTrue();
        var rejected = controller.TryReserve(message, ActorType.Command, out _);

        rejected.Reason.Should().Be(ActorAdmissionReason.GlobalByteLimit);
        controller.CurrentMessageCount.Should().Be(1);
        controller.CurrentByteCount.Should().Be(10);
        controller.Release(accepted);
    }

    [Fact]
    public void Enforce_RejectsActorTypeLimit_AndLeavesGlobalCapacityAvailable()
    {
        var controller = new ActorAdmissionController(CreateEnforcedOptions(
            globalMessages: 4,
            globalBytes: 40,
            typeMessages: 1,
            typeBytes: 20));
        var message = new SizedActorMessage(10);

        controller.TryReserve(message, ActorType.Command, out var command).Accepted.Should().BeTrue();
        controller.TryReserve(message, ActorType.Command, out _).Reason
            .Should().Be(ActorAdmissionReason.ActorTypeMessageLimit);
        controller.TryReserve(message, ActorType.Query, out var query).Accepted.Should().BeTrue();

        controller.CurrentMessageCount.Should().Be(2);
        controller.CurrentByteCount.Should().Be(20);
        controller.Release(query);
        controller.Release(command);
    }

    [Fact]
    public void Enforce_RejectsActorTypeByteLimit_AndOversizedPayload()
    {
        var options = CreateEnforcedOptions(
            globalMessages: 4,
            globalBytes: 100,
            typeMessages: 4,
            typeBytes: 15);
        options.MaximumPayloadBytes = 20;
        var controller = new ActorAdmissionController(options);

        controller.TryReserve(new SizedActorMessage(10), ActorType.Event, out var accepted)
            .Accepted.Should().BeTrue();
        controller.TryReserve(new SizedActorMessage(10), ActorType.Event, out _).Reason
            .Should().Be(ActorAdmissionReason.ActorTypeByteLimit);
        controller.TryReserve(new SizedActorMessage(21), ActorType.Query, out _).Reason
            .Should().Be(ActorAdmissionReason.PayloadTooLarge);

        controller.CurrentMessageCount.Should().Be(1);
        controller.CurrentByteCount.Should().Be(10);
        controller.Release(accepted);
    }

    [Fact]
    public void Enforce_ConcurrentReservations_NeverExceedConfiguredCapacity()
    {
        const int limit = 32;
        var controller = new ActorAdmissionController(CreateEnforcedOptions(
            globalMessages: limit,
            globalBytes: limit * 10,
            typeMessages: limit,
            typeBytes: limit * 10));
        var charges = new ConcurrentBag<ActorAdmissionCharge>();

        Parallel.For(0, 2_000, _ =>
        {
            if (controller.TryReserve(new SizedActorMessage(10), ActorType.Command, out var charge).Accepted)
                charges.Add(charge);
        });

        charges.Should().HaveCount(limit);
        controller.CurrentMessageCount.Should().Be(limit);
        controller.CurrentByteCount.Should().Be(limit * 10);
        foreach (var charge in charges)
            controller.Release(charge);
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
    }

    [Fact]
    public void ObserveOnly_TracksAndReleasesBacklogWithoutRejecting()
    {
        var reasons = new ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ActorRuntimeMetrics.MeterName
                && instrument.Name == "ifm.actor.admission.would_reject")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "reason" && tag.Value is string reason)
                    reasons.Add(reason);
            }
        });
        listener.Start();

        var options = new ActorAdmissionOptions
        {
            Mode = ActorAdmissionMode.ObserveOnly,
            GlobalMessageLimit = 1,
            GlobalByteLimit = 100,
            MaximumPayloadBytes = 100,
            DefaultMailboxMessageLimit = 1
        };
        var controller = new ActorAdmissionController(options);
        using var queue = new ActorThreadQueueV2(controller, capacity: 4);
        queue.SetId(new ActorThreadId(ActorType.Command, "AdmissionTest", "1"));
        queue.Start();
        var scheduled = (IScheduledActorThreadQueue)queue;

        scheduled.TryWrite(new SizedActorMessage(10), default).Should().BeTrue();
        scheduled.TryWrite(new SizedActorMessage(10), default).Should().BeTrue();

        controller.CurrentMessageCount.Should().Be(2);
        controller.CurrentByteCount.Should().Be(20);
        reasons.Should().Contain("global_message_limit");

        scheduled.TryRead(out _).Should().BeTrue();
        scheduled.TryRead(out _).Should().BeTrue();
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
    }

    static ActorAdmissionOptions CreateEnforcedOptions(
        long globalMessages,
        long globalBytes,
        long typeMessages,
        long typeBytes)
        => new()
        {
            Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = globalMessages,
            GlobalByteLimit = globalBytes,
            MaximumPayloadBytes = (int)Math.Min(globalBytes, int.MaxValue),
            DefaultActorTypeMessageLimit = typeMessages,
            DefaultActorTypeByteLimit = typeBytes,
            DefaultMailboxMessageLimit = (int)Math.Min(globalMessages, int.MaxValue)
        };

    sealed class SizedActorMessage(int admissionSizeBytes) : IActorMessage
    {
        public int AdmissionSizeBytes { get; } = admissionSizeBytes;
        public ActorSubject Subject { get; } = new(ActorType.Command, "AdmissionTest", "Run", "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
