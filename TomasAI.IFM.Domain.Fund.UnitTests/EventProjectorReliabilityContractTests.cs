using FluentAssertions;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class EventProjectorReliabilityContractTests
{
    [Fact]
    public void Effect_identity_is_stable_across_retries_and_distinct_per_effect()
    {
        var firstAttempt = new EventProjectorEffectIdentity(
            "FundEventProjector",
            42,
            EventProjectorEffectKind.TargetProjection);
        var retry = new EventProjectorEffectIdentity(
            "FundEventProjector",
            42,
            EventProjectorEffectKind.TargetProjection);
        var completion = new EventProjectorEffectIdentity(
            "FundEventProjector",
            42,
            EventProjectorEffectKind.CompletedPublication);

        retry.MessageId.Should().Be(firstAttempt.MessageId);
        completion.MessageId.Should().NotBe(firstAttempt.MessageId);
        firstAttempt.MessageId.Should().StartWith("ifm-projector-").And.HaveLength(78);
    }

    [Fact]
    public void Effect_identity_rejects_non_persisted_or_ambiguous_effects()
    {
        var missingProjector = () => new EventProjectorEffectIdentity(" ", 1, EventProjectorEffectKind.TargetProjection);
        var missingEvent = () => new EventProjectorEffectIdentity("FundEventProjector", 0, EventProjectorEffectKind.TargetProjection);
        var missingEffect = () => new EventProjectorEffectIdentity("FundEventProjector", 1, EventProjectorEffectKind.None);

        missingProjector.Should().Throw<ArgumentException>();
        missingEvent.Should().Throw<ArgumentOutOfRangeException>();
        missingEffect.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Reliability_options_are_bounded_and_default_to_valid_values()
    {
        new EventProjectorReliabilityOptions().Validate().Should().NotBeNull();

        var invalidBatch = new EventProjectorReliabilityOptions { RecoveryBatchSize = 0 };
        var invalidConcurrency = new EventProjectorReliabilityOptions { RecoveryStreamConcurrency = 33 };
        var invalidLease = new EventProjectorReliabilityOptions { ClaimLeaseDuration = TimeSpan.Zero };
        var outboxWithoutFence = new EventProjectorReliabilityOptions { TransactionalOutboxEnabled = true };
        var invalidMetricsPolling = new EventProjectorReliabilityOptions { MetricsPollingInterval = TimeSpan.FromMilliseconds(500) };

        invalidBatch.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
        invalidConcurrency.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
        invalidLease.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
        outboxWithoutFence.Invoking(options => options.Validate()).Should().Throw<InvalidOperationException>();
        invalidMetricsPolling.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Projector_metrics_emit_low_cardinality_flow_and_backlog_measurements()
    {
        const string projectorName = "FundEventProjector.MetricsTest";
        var measurements = new ConcurrentBag<(string Name, double Value, string Projector)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == EventProjectorMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, ProjectorTag(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, ProjectorTag(tags))));
        listener.Start();

        EventProjectorMetrics.RegisterProjector(projectorName, 3);
        EventProjectorMetrics.UpdateSnapshot(
            projectorName,
            new TomasAI.IFM.Shared.EventProjector.ReadModels.EventProjectorOperationalSnapshotReadModel(
                7, DateTime.UtcNow.AddSeconds(-10), 2, 1, 1, 4, DateTime.UtcNow.AddSeconds(-5), 3),
            DateTime.UtcNow);
        EventProjectorMetrics.RecordEvent(projectorName, "claimed");
        EventProjectorMetrics.WorkerBusy(projectorName);
        try
        {
            listener.RecordObservableInstruments();
        }
        finally
        {
            EventProjectorMetrics.WorkerAvailable(projectorName);
            EventProjectorMetrics.UnregisterProjector(projectorName);
        }

        measurements.Should().Contain(item => item.Name == "ifm.event_projector.events" && item.Projector == projectorName);
        measurements.Should().Contain(item => item.Name == "ifm.event_projector.backlog.pending" && item.Value == 7 && item.Projector == projectorName);
        measurements.Should().Contain(item => item.Name == "ifm.event_projector.outbox.pending" && item.Value == 4 && item.Projector == projectorName);
        measurements.Should().Contain(item => item.Name == "ifm.event_projector.worker.utilization" && item.Value > 0 && item.Projector == projectorName);

        static string ProjectorTag(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "projector")
                    return tag.Value?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }
    }

    [Fact]
    public void Descriptor_requires_an_explicit_idempotency_strategy()
    {
        var create = () => new EventProjectionDescriptor(
            typeof(FundCreatedEvent),
            EventProjectionIdempotencyStrategy.Unspecified,
            static (_, _) => ValueTask.FromResult(new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied)),
            static _ => null,
            static (_, _) => null);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Execution_context_rejects_an_effect_identity_from_another_event()
    {
        var mismatchedEffect = new EventProjectorEffectIdentity(
            "FundEventProjector",
            43,
            EventProjectorEffectKind.TargetProjection);

        var create = () => new ProjectionExecutionContext(
            "FundEventProjector",
            42,
            7,
            mismatchedEffect,
            Guid.NewGuid(),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            CancellationToken.None);

        create.Should().Throw<ArgumentException>();
    }
}
