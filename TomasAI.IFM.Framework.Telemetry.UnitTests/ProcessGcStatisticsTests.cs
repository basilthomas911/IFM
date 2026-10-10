using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Framework.Telemetry.Metrics;
using Xunit;

namespace TomasAI.IFM.Framework.Telemetry.UnitTests;

public sealed class ProcessGcStatisticsTests
{
    [Fact]
    public async Task History_preserves_process_totals_samples_and_final_shutdown_observation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-gc-test", Guid.NewGuid().ToString("N"));
        var config = Configuration(root);
        var baseline = GC.CollectionCount(0);
        var recorder = ProcessGcStatisticsRecorder.Start(config, "Fixture")!;
        try
        {
            await Task.Delay(1250);
            await recorder.DisposeAsync();
            var samples = File.ReadAllLines(recorder.HistoryPath).Select(line => JsonSerializer.Deserialize<ProcessGcSnapshot>(line)!).ToArray();
            Assert.Equal("Started", samples[0].Phase);
            Assert.Equal("Stopped", samples[^1].Phase);
            Assert.Contains(samples, item => item.Phase == "Sample");
            Assert.All(samples, item => Assert.Equal(ProcessGcRunIdentity.InstanceId, item.ProcessRunId));
            Assert.All(samples, item => Assert.Equal(ProcessGcRunIdentity.StartedUtc, item.ProcessStartedUtc));
            Assert.True(samples[0].Gen0CollectionsSinceStart >= baseline);
            Assert.True(samples[^1].AllocatedBytesSinceStartApproximate >= samples[0].AllocatedBytesSinceStartApproximate);
            Assert.True(samples[^1].PauseMillisecondsSinceStart >= samples[0].PauseMillisecondsSinceStart);
            Assert.Equal(3, samples.Select(item => item.Phase).Distinct().Count());
        }
        finally
        {
            await recorder.DisposeAsync();
            foreach (var file in Directory.GetFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    [Fact]
    public async Task Otel_observations_include_each_generations_lifetime_total()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-gc-test", Guid.NewGuid().ToString("N"));
        var counts = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ProcessGcStatisticsRecorder.MeterName && instrument.Name == "ifm.process.gc.collections")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            foreach (var tag in tags) if (tag.Key == "gc.heap.generation") counts[(string)tag.Value!] = value;
        });
        listener.Start();
        await using var recorder = ProcessGcStatisticsRecorder.Start(Configuration(root), "Fixture")!;
        try
        {
            listener.RecordObservableInstruments();
            Assert.Equal(3, counts.Count);
            for (var generation = 0; generation <= 2; generation++)
                Assert.InRange(counts[$"gen{generation}"], 0, GC.CollectionCount(generation));
        }
        finally
        {
            await recorder.DisposeAsync();
            foreach (var file in Directory.GetFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    static IConfiguration Configuration(string root) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Telemetry:GcHistory:OutputDirectory"] = root,
        ["Telemetry:GcHistory:SampleIntervalSeconds"] = "1"
    }).Build();
}
