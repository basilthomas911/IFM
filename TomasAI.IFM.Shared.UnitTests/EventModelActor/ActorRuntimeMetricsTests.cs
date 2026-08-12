using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorRuntimeMetricsTests
{
    [Fact]
    public void WorkerGauges_ReportCapacityAvailabilityAndUtilization()
    {
        var measurements = new ConcurrentDictionary<string, double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ActorRuntimeMetrics.MeterName
                && instrument.Name.StartsWith("ifm.actor.worker.", StringComparison.Ordinal))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            measurements[instrument.Name] = value);
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
            measurements[instrument.Name] = value);
        listener.Start();

        ActorRuntimeMetrics.RegisterWorkerPool(4);
        ActorRuntimeMetrics.RecordWorkerBusy();
        try
        {
            listener.RecordObservableInstruments();

            measurements["ifm.actor.worker.capacity"].Should().BeGreaterThanOrEqualTo(4);
            measurements["ifm.actor.worker.busy"].Should().BeGreaterThanOrEqualTo(1);
            measurements["ifm.actor.worker.available"].Should().Be(
                measurements["ifm.actor.worker.capacity"]
                - measurements["ifm.actor.worker.busy"]);
            measurements["ifm.actor.worker.utilization"].Should().BeApproximately(
                measurements["ifm.actor.worker.busy"] * 100
                / measurements["ifm.actor.worker.capacity"],
                0.000_001);
        }
        finally
        {
            ActorRuntimeMetrics.RecordWorkerAvailable();
            ActorRuntimeMetrics.UnregisterWorkerPool(4);
        }
    }

    [Fact]
    public void MailboxLifecycleMetric_IsBalancedAcrossDisposePaths()
    {
        var measurements = new ConcurrentBag<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ActorRuntimeMetrics.MeterName
                && instrument.Name == "ifm.actor.mailbox.active")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "actor.type" && Equals(tag.Value, nameof(ActorType.Realtime)))
                    measurements.Add(value);
            }
        });
        listener.Start();

        using (var started = new ActorThreadQueueV2())
        {
            started.SetId(new ActorThreadId(ActorType.Realtime, "MetricsTest", "1"));
            started.Start();
            started.Dispose();
        }

        using var neverStarted = new ActorThreadQueueV2();
        neverStarted.Dispose();

        measurements.Should().BeEquivalentTo([1L, -1L]);
        measurements.Sum().Should().Be(0);
    }

    [Fact]
    public void QueueLifecycleAndMessageFlow_EmitBoundedRuntimeMeasurements()
    {
        var measurements = new ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ActorRuntimeMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
            measurements.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            measurements.Add(instrument.Name));
        listener.Start();

        using var queue = new ActorThreadQueueV2(capacity: 4);
        queue.SetId(new ActorThreadId(ActorType.Command, "MetricsTest", "entity-id-is-not-a-tag"));
        queue.Start();

        var scheduled = (IScheduledActorThreadQueue)queue;
        scheduled.TryWrite(new TestActorMessage(), default).Should().BeTrue();
        scheduled.TrySchedule().Should().BeTrue();
        scheduled.TryRead(out var message).Should().BeTrue();
        message.Should().NotBeNull();
        scheduled.CompleteDrain().Should().BeFalse();

        measurements.Should().Contain("ifm.actor.mailbox.active");
        measurements.Should().Contain("ifm.actor.messages.accepted");
        measurements.Should().Contain("ifm.actor.mailbox.depth");
        measurements.Should().Contain("ifm.actor.mailbox.enqueue_wait.duration");
        measurements.Should().Contain("ifm.actor.mailbox.queue_wait.duration");
    }

    sealed class TestActorMessage : IActorMessage
    {
        public ActorSubject Subject { get; } = new(ActorType.Command, "MetricsTest", "Run", "1");
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
