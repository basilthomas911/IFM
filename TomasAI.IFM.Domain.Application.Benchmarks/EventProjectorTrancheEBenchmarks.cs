using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Application.Benchmarks;

/// <summary>
/// CPU/allocation lower bounds for Tranche E instrumentation and outbox payload handling. Storage, NATS, and target
/// database latency are deliberately excluded and are covered by the real-topology integration gate.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 2, iterationCount: 5)]
public class EventProjectorTrancheEBenchmarks
{
    const string ProjectorName = "ApplicationEventProjector";
    const int Operations = 256;
    readonly ApplicationStartupEvent _sourceEvent = new()
    {
        Id = Guid.Parse("846f4f52-78b8-4bd7-aa5f-a7498c39ddb3"),
        CommandId = Guid.Parse("71ad7af5-d577-48a0-991f-a3932165f63f"),
        AggregateId = "tranche-e-benchmark",
        EventSource = nameof(EventProjectorTrancheEBenchmarks),
        ReceivedOn = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
        CreatedOn = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
        CreatedBy = "benchmark"
    };
    MeterListener? _listener;

    [Params(false, true)]
    public bool MetricsListenerEnabled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!MetricsListenerEnabled)
            return;
        _listener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == EventProjectorMetrics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>(static (_, _, _, _) => { });
        _listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
        _listener.Start();
    }

    [GlobalCleanup]
    public void Cleanup() => _listener?.Dispose();

    [Benchmark]
    public long StageTelemetryBatch()
    {
        long checksum = 0;
        for (var index = 0; index < Operations; index++)
        {
            var started = EventProjectorMetrics.GetTimestamp();
            checksum += index;
            EventProjectorMetrics.RecordEvent(ProjectorName, "applied");
            EventProjectorMetrics.RecordStage(
                ProjectorName,
                EventProjectorStageType.ApplyProjection,
                "completed",
                started);
        }
        return checksum;
    }

    [Benchmark]
    public int OutboxPayloadRoundTripBatch()
    {
        var totalBytes = 0;
        for (var index = 1; index <= Operations; index++)
        {
            var identity = new EventProjectorEffectIdentity(
                ProjectorName,
                index,
                EventProjectorEffectKind.CompletedPublication);
            var message = EventProjectorOutboxSerializer.Serialize(_sourceEvent, identity);
            totalBytes += message.EventPayload.Length;
            _ = EventProjectorOutboxSerializer.Deserialize(message.EventTypeName, message.EventPayload);
        }
        return totalBytes;
    }
}
