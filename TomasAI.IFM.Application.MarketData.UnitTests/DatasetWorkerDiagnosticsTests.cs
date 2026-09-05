using System.Diagnostics;
using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetWorkerDiagnosticsTests
{
    static readonly DateTime Observed = new(2026, 9, 4, 14, 0, 0, DateTimeKind.Utc);
    static readonly Guid Generation = Guid.Parse("F7B30D93-2A77-4659-8706-EEC51D7395DF");

    [Fact]
    public async Task Diagnostic_control_frame_roundtrip_preserves_real_native_and_managed_evidence()
    {
        var diagnostics = Capture();
        using var stream = new MemoryStream();
        var frame = Frame(diagnostics);

        await DatasetWorkerFrameCodec.WriteAsync(stream, frame, 256 * 1024, CancellationToken.None);
        stream.Position = 0;
        var decoded = await DatasetWorkerFrameCodec.ReadAsync(stream, 256 * 1024, CancellationToken.None);

        decoded.Diagnostics.Should().BeEquivalentTo(diagnostics);
        decoded.Diagnostics!.RecordsProduced.Should().Be(100);
        decoded.Diagnostics.RecordsConsumed.Should().Be(90);
        decoded.Diagnostics.LastHeartbeatAgeTicks.Should().Be(TimeSpan.FromSeconds(10).Ticks);
        decoded.Diagnostics.Aggregation!.RecordsCompleted.Should().Be(80);
        decoded.Diagnostics.Drain!.NativeReadCallCount.Should().Be(7);
        stream.Length.Should().BeLessThan(16 * 1024, "health diagnostics are a bounded control-plane observation");
    }

    [Fact]
    public void Forwarded_waiting_drain_with_buffered_records_is_confirmed_as_stalled()
    {
        var diagnostics = Capture() with { Drain = Capture().Drain! with { Stage = FeedDrainStage.WaitingForNativeSignal } };
        var evaluator = new DatabentoDatasetHealthEvaluator(TimeSpan.FromMinutes(1));
        var feed = diagnostics.ToWatchdogStatus(Manifest().GetRegistrations(), true);

        var first = evaluator.Evaluate(feed, Observed);
        var confirmed = evaluator.Evaluate(feed, Observed.AddMinutes(1));

        first.State.Should().Be(DatabentoDatasetState.Suspect);
        confirmed.State.Should().Be(DatabentoDatasetState.Down);
        confirmed.Reason.Should().Be(DatabentoDatasetFailureReason.NativeDrainStalled);
    }

    [Fact]
    public void Forwarded_managed_publish_backpressure_is_not_mistaken_for_quiet_provider()
    {
        var baseline = Capture();
        var diagnostics = baseline with
        {
            Drain = baseline.Drain! with { Stage = FeedDrainStage.PublishingManagedBatch, ManagedBatchPublishActive = true }
        };
        var evaluator = new DatabentoDatasetHealthEvaluator(TimeSpan.FromMinutes(1));
        var feed = diagnostics.ToWatchdogStatus(Manifest().GetRegistrations(), true);

        evaluator.Evaluate(feed, Observed);
        var confirmed = evaluator.Evaluate(feed, Observed.AddMinutes(1));

        confirmed.State.Should().Be(DatabentoDatasetState.Down);
        confirmed.Reason.Should().Be(DatabentoDatasetFailureReason.ManagedChannelBlocked);
    }

    [Fact]
    public void Forwarded_inflight_record_duration_triggers_aggregation_stall_detection()
    {
        var baseline = Capture();
        var diagnostics = baseline with
        {
            Aggregation = baseline.Aggregation! with
            {
                CurrentProcessingDurationTicks = TimeSpan.FromMinutes(2).Ticks,
                InFlightRecord = new("GLBX.MDP3", "ES20261218", "Trade", 1, 1, 87, Observed)
            }
        };
        var evaluator = new DatabentoDatasetHealthEvaluator(TimeSpan.FromMinutes(1));

        var result = evaluator.Evaluate(diagnostics.ToWatchdogStatus(Manifest().GetRegistrations(), true), Observed);

        result.State.Should().Be(DatabentoDatasetState.Down);
        result.Reason.Should().Be(DatabentoDatasetFailureReason.AggregationRecordStalled);
    }

    [Fact]
    public void Missing_native_evidence_is_incomplete_and_down_never_fabricated_up()
    {
        var diagnostics = DatasetWorkerDiagnostics.Capture(Manifest(), Epoch(), null, "native unavailable", Observed);

        diagnostics.Complete.Should().BeFalse();
        diagnostics.Operational.Should().BeFalse();
        var status = diagnostics.ToWatchdogStatus(Manifest().GetRegistrations(), true);
        status.MajorStatus.Should().Be(DatabentoMajorStatus.Down);
        status.NativeState.Should().Be("DiagnosticsUnavailable");
        status.LastHeartbeatAge.Should().Be(TimeSpan.MaxValue);
        status.FailureDetail.Should().Contain("native unavailable");
    }

    [Fact]
    public void Complete_diagnostics_can_report_operational_failure_without_becoming_incomplete()
    {
        var diagnostics = Capture() with { RingOverruns = 1 };
        var evaluator = new DatabentoDatasetHealthEvaluator(TimeSpan.FromMinutes(1));

        diagnostics.Complete.Should().BeTrue();
        diagnostics.Operational.Should().BeFalse();
        var result = evaluator.Evaluate(diagnostics.ToWatchdogStatus(Manifest().GetRegistrations(), true), Observed);
        result.Reason.Should().Be(DatabentoDatasetFailureReason.NativeRingOverrun);
    }

    [Fact]
    public async Task Diagnostics_from_wrong_generation_are_rejected_by_control_boundary()
    {
        using var stream = new MemoryStream();
        var frame = Frame(Capture()) with { GenerationId = Guid.NewGuid() };

        var write = () => DatasetWorkerFrameCodec.WriteAsync(stream, frame, 256 * 1024, CancellationToken.None).AsTask();

        await write.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Real_synthetic_worker_reports_nonzero_native_drain_and_aggregation_counters()
    {
        await using var supervisor = new DatasetWorkerProcessSupervisor(new DatabentoStage3Options
        {
            WorkerHandshakeTimeout = TimeSpan.FromSeconds(10),
            WorkerCommandTimeout = TimeSpan.FromSeconds(5),
            WorkerGracefulStopTimeout = TimeSpan.FromSeconds(5)
        });
        var started = await supervisor.StartAsync(new()
        {
            ExecutablePath = DotNetHost(),
            PrefixArguments = [typeof(DatasetWorkerAssemblyMarker).Assembly.Location],
            Dataset = "GLBX.MDP3", ValueDate = new(2026, 9, 4), WorkerInstanceId = Guid.NewGuid(),
            GenerationId = Guid.NewGuid(), Manifest = Manifest()
        });
        DatasetWorkerControlFrame health;
        var timer = Stopwatch.StartNew();
        do
        {
            health = await supervisor.GetHealthAsync();
            if (health.Diagnostics is { RecordsConsumed: > 0, Aggregation: { RecordsCompleted: > 0, SourceQuoteRecords: > 0 } }) break;
            await Task.Delay(50);
        } while (timer.Elapsed < TimeSpan.FromSeconds(10));

        health.Healthy.Should().BeTrue(health.Detail);
        health.Diagnostics.Should().NotBeNull();
        var evidence = health.Diagnostics!;
        evidence.Complete.Should().BeTrue(evidence.FailureDetail);
        evidence.GenerationId.Should().Be(started.GenerationId);
        evidence.RecordsProduced.Should().BePositive();
        evidence.RecordsConsumed.Should().BePositive();
        evidence.RingCapacity.Should().BePositive();
        evidence.Drain!.NativeReadCallCount.Should().BePositive();
        evidence.Aggregation!.RecordsCompleted.Should().BePositive();
        evidence.Aggregation.SourceQuoteRecords.Should().BePositive();
        supervisor.Current.Diagnostics.Should().Be(evidence);
    }

    static DatasetWorkerDiagnostics Capture() => DatasetWorkerDiagnostics.Capture(Manifest(), Epoch(),
        new(20_000_000_000, 1, [new DatabentoNativeFeedWatchdogStatus(1, 1, 1, 1,
            FeedState.Running, DatabentoFeedStatus.Ok, true, true, 1, 1, 3, 50,
            10_000_000_000, 19_000_000_000, 100, 90, 1024, 10, 50, 0, "GLBX.MDP3", string.Empty)]),
        string.Empty, Observed);

    static DatabentoMarketDataEpochHealth Epoch() => new(new(2026, 9, 4), true, true, 1, 1, true,
        40, 40, 0, 0, [new("ES20261218", AssetTypeId.Futures, true, true, true)],
        [new("GLBX.MDP3", Generation, new FeedHealthSnapshot(FeedState.Running, DatabentoFeedStatus.Ok,
            1024, 10, 50, 100, 90, 9, 0, 0, 0, null)
        {
            TransportReady = true, TradingReady = true, InstrumentCount = 1, BaselineReadyInstrumentCount = 1,
            ChannelBatchCapacity = 4, ChannelBatchCount = 1,
            DrainDiagnostics = new()
            {
                Stage = FeedDrainStage.RoutingNativeRecord, NativeReadCallCount = 7,
                LastNativeReadRecordCount = 10, LastNativeReadFirstSequence = 80, LastNativeReadLastSequence = 90,
                LastNativeReadRecordsRouted = 10, CurrentNativeReadRecordIndex = 9, CurrentRecordKind = "Trade",
                CurrentPublisherId = 1, CurrentInstrumentId = 1, CurrentSourceSequence = 90,
                ManagedBatchPublishActive = false, ManagedBatchPublishRecordCount = 10,
                ManagedBatchPublisherId = 1, ManagedBatchInstrumentId = 1
            }
        }, new TickAggregationMetricsSnapshot(40, 40, 4, 40, 40, 0, 0, 0, 0, 0, 0, 0, 1, 1)
        {
            RecordsStarted = 81, RecordsCompleted = 80, CurrentStage = TickAggregationProcessingStage.TradeUpdate
        })]);

    static DatasetSubscriptionManifest Manifest() => new("GLBX.MDP3", new(2026, 9, 4), 1,
        [DatasetSubscriptionContract.FromRegistration(new()
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6", AssetTypeId = AssetTypeId.Futures,
            RootSymbol = "ES", Dataset = "GLBX.MDP3", OnTheRun = true, Rollover = true
        })]);

    static DatasetWorkerControlFrame Frame(DatasetWorkerDiagnostics diagnostics) => new()
    {
        Kind = DatasetWorkerMessageKind.HealthSnapshot, WorkerInstanceId = Guid.NewGuid(), Dataset = diagnostics.Dataset,
        ValueDate = new(2026, 9, 4), GenerationId = diagnostics.GenerationId, CorrelationId = Guid.NewGuid(),
        Sequence = 1, BootstrapToken = new string('A', 64), Diagnostics = diagnostics
    };

    static string DotNetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var candidate = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (File.Exists(candidate)) return candidate;
        throw new InvalidOperationException("The test could not resolve the absolute dotnet host path.");
    }
}
