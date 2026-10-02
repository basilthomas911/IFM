using System.Diagnostics;
using System.Text.Json;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.QualificationHost;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

[CollectionDefinition("Dataset containment qualification", DisableParallelization = true)]
public sealed class DatasetContainmentQualificationCollection;

[Collection("Dataset containment qualification")]
public sealed class DatasetWorkerContainmentQualificationTests(ITestOutputHelper output)
{
    static readonly DateOnly ValueDate = TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetOperational(DateTimeOffset.UtcNow);

    [Fact]
    public void Default_worker_start_deadline_contains_catalog_retries_feed_start_and_protocol_margin()
    {
        var runtime = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3"),
            Contracts = []
        };
        var backoff = Enumerable.Range(1, runtime.CatalogQueryAttempts - 1)
            .Sum(attempt => runtime.CatalogQueryRetryDelay.Ticks * attempt);
        var required = runtime.ProviderQueryTimeout * runtime.CatalogQueryAttempts
            + TimeSpan.FromTicks(backoff) + runtime.FeedStartTimeout + TimeSpan.FromSeconds(10);
        Assert.True(new DatabentoStage3Options().WorkerStartTimeout > required,
            $"Worker startup must outlive its bounded inner phases ({required}).");
        Assert.Throws<InvalidOperationException>(() =>
            (new DatabentoStage3Options { WorkerStartTimeout = TimeSpan.FromMinutes(4) }).Validate());
    }

    [Fact]
    public async Task Worker_start_acknowledgement_after_old_30_second_limit_succeeds_and_process_tree_is_cleaned()
    {
        var request = Request(helper: true);
        request = request with { PrefixArguments = [.. request.PrefixArguments, "--startup-delay-ms", "31000"] };
        await using var owner = new DatasetWorkerProcessSupervisor(Options() with
        { WorkerStartTimeout = new DatabentoStage3Options().WorkerStartTimeout });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        var elapsed = Stopwatch.StartNew();
        var started = await owner.StartAsync(request, deadline.Token);
        using var worker = Process.GetProcessById(started.ProcessId);
        using var descendant = Process.GetProcessById(int.Parse(started.Detail));
        try
        {
            Assert.True(started.Healthy);
            Assert.True(elapsed.Elapsed > TimeSpan.FromSeconds(30));
            await owner.StopAsync(deadline.Token);
            // Full owner disposal closes the Windows job, containing descendants after graceful leader exit.
            await owner.DisposeAsync();
            await worker.WaitForExitAsync(deadline.Token);
            await descendant.WaitForExitAsync(deadline.Token);
            Assert.True(worker.HasExited);
            Assert.True(descendant.HasExited);
        }
        finally
        {
            await TerminateOwnedAsync(descendant);
            await TerminateOwnedAsync(worker);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expired_worker_start_or_caller_cancellation_is_distinguished_and_child_is_terminated(bool cancelCaller)
    {
        var request = Request(helper: true);
        request = request with { PrefixArguments = [.. request.PrefixArguments, "--startup-delay-ms", "3000"] };
        await using var owner = new DatasetWorkerProcessSupervisor(Options() with
        { WorkerStartTimeout = cancelCaller ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(100) });
        using var caller = new CancellationTokenSource();
        if (cancelCaller) caller.CancelAfter(TimeSpan.FromMilliseconds(500));
        if (cancelCaller)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.StartAsync(request, caller.Token));
        }
        else
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => owner.StartAsync(request, caller.Token));
            Assert.Contains("StartManifest", error.Message);
            Assert.Contains("StartAccepted", error.Message);
            Assert.Contains("GLBX.MDP3", error.Message);
            Assert.Contains($"PID={owner.Current.ProcessId}", error.Message);
            Assert.Contains("manifestRevision=1", error.Message);
            Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        }
        Assert.False(owner.Current.Running);
        Assert.True(owner.Current.ForcedTermination);
    }

    [Fact]
    public async Task Forced_stop_terminates_exact_worker_and_descendant_processes()
        => await StopTreeAsync(hang: true);

    [LinuxQualificationFact]
    public async Task Graceful_leader_exit_still_terminates_sigterm_resistant_descendant()
        => await StopTreeAsync(hang: false);

    async Task StopTreeAsync(bool hang)
    {
        await using var owner = new DatasetWorkerProcessSupervisor(Options());
        var started = await owner.StartAsync(Request(helper: true));
        using var worker = Process.GetProcessById(started.ProcessId);
        using var descendant = Process.GetProcessById(int.Parse(started.Detail));
        try
        {
            Assert.False(descendant.HasExited);
            if (hang) await owner.HangAsync();

            var stopped = await owner.StopAsync();

            Assert.True(stopped.ForcedTermination);
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await descendant.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(worker.HasExited);
            Assert.True(descendant.HasExited);
            output.WriteLine($"Platform={Environment.OSVersion}; worker={worker.Id}; descendant={descendant.Id}; both exited.");
        }
        finally
        {
            await TerminateOwnedAsync(descendant);
            await TerminateOwnedAsync(worker);
        }
    }

    [WindowsQualificationFact]
    public async Task Abrupt_supervisor_process_exit_closes_job_and_kills_worker_and_descendant()
    {
        var launch = new ProcessStartInfo(DotNetHost())
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        launch.ArgumentList.Add(typeof(QualificationHostMarker).Assembly.Location);
        launch.ArgumentList.Add("--parent");
        using var parent = Process.Start(launch)!;
        Process? worker = null;
        Process? descendant = null;
        try
        {
            var line = await parent.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.False(string.IsNullOrWhiteSpace(line));
            var tree = JsonSerializer.Deserialize<QualificationProcessTree>(line!)!;
            Assert.Equal(parent.Id, tree.ParentId);
            worker = Process.GetProcessById(tree.WorkerId);
            descendant = Process.GetProcessById(tree.DescendantId);

            // Deliberately kill ONLY the owning parent; its job-object close must do the rest.
            parent.Kill(entireProcessTree: false);

            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await descendant.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(worker.HasExited);
            Assert.True(descendant.HasExited);
            output.WriteLine($"Abrupt parent={parent.Id}; worker={worker.Id}; descendant={descendant.Id}; no survivor.");
        }
        finally
        {
            if (descendant is not null) { await TerminateOwnedAsync(descendant); descendant.Dispose(); }
            if (worker is not null) { await TerminateOwnedAsync(worker); worker.Dispose(); }
            await TerminateOwnedAsync(parent);
        }
    }

    [Fact]
    public async Task Repeated_native_worker_replacement_keeps_parent_resources_bounded()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), admissions);
        var current = await recovery.StartOwnedAsync(Request());
        for (var warmup = 0; warmup < 2; warmup++)
        {
            Assert.True((await recovery.ReplaceProcessAsync(Reset(current), CancellationToken.None)).Succeeded);
            current = Assert.Single(recovery.Current);
        }
        using var host = Process.GetCurrentProcess();
        var managedBefore = GC.GetTotalMemory(forceFullCollection: true);
        host.Refresh();
        var handlesBefore = host.HandleCount;
        var privateBefore = host.PrivateMemorySize64;
        var threadsBefore = host.Threads.Count;
        const int replacements = 8;
        for (var index = 0; index < replacements; index++)
        {
            using var previous = Process.GetProcessById(current.ProcessId);
            var result = await recovery.ReplaceProcessAsync(Reset(current), CancellationToken.None);
            Assert.True(result.Succeeded, result.Detail);
            current = Assert.Single(recovery.Current);
            Assert.True(previous.HasExited);
            Assert.NotEqual(previous.Id, current.ProcessId);
            Assert.True(current.Healthy, current.Detail);
            Assert.True(admissions.TryGet(current.Dataset, out var admitted));
            Assert.Equal(current.GenerationId, admitted.GenerationId);
        }
        var managedAfter = GC.GetTotalMemory(forceFullCollection: true);
        host.Refresh();
        var handlesGrowth = host.HandleCount - handlesBefore;
        var privateGrowth = host.PrivateMemorySize64 - privateBefore;
        var threadsGrowth = host.Threads.Count - threadsBefore;
        output.WriteLine($"Replacements={replacements}; handles={handlesGrowth:+#;-#;0}; threads={threadsGrowth:+#;-#;0}; "
            + $"managedBytes={managedAfter - managedBefore}; privateBytes={privateGrowth}.");
        Assert.InRange(handlesGrowth, int.MinValue, 16);
        Assert.InRange(threadsGrowth, int.MinValue, 8);
        Assert.InRange(managedAfter - managedBefore, long.MinValue, 16L * 1024 * 1024);
        Assert.InRange(privateGrowth, long.MinValue, 64L * 1024 * 1024);
        await recovery.StopAllAsync();
        Assert.Empty(recovery.Current);
        Assert.False(admissions.TryGet(current.Dataset, out _));
    }

    [Fact]
    public async Task Timed_out_hard_containment_keeps_worker_startup_fenced_after_late_cleanup()
    {
        var options = Options() with
        {
            WorkerStartTimeout = TimeSpan.FromSeconds(2),
            WorkerGracefulStopTimeout = TimeSpan.FromMilliseconds(100)
        };
        var admissions = new DatasetWorkerAdmissionRegistry();
        DatasetWorkerProcessSupervisor? supervisor = null;
        await using var recovery = new DatasetWorkerProcessRecoveryService(options, admissions,
            supervisorFactory: configured => supervisor = new DatasetWorkerProcessSupervisor(configured));
        var request = Request();
        var started = await recovery.StartOwnedAsync(request);
        await supervisor!.HangAsync();
        var resetting = recovery.ResetOwnedAsync(Reset(started), CancellationToken.None);
        using var waiting = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (admissions.TryGet(started.Dataset, out _))
            await Task.Delay(10, waiting.Token);

        var contained = await recovery.ContainForHardRecoveryAsync(
            TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.False(contained.Isolated);
        Assert.IsType<TimeoutException>(contained.Failure);
        Assert.Contains(started.ProcessId, contained.ProcessIds);
        await resetting;
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (recovery.Current.Count != 0)
            await Task.Delay(10, cleanup.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.StartCandidateAsync(Request()));
        output.WriteLine("Late worker cleanup completed, but the unverified hard-containment episode stayed fenced.");
    }

    [Fact]
    public async Task Stopped_stage_3_can_roll_back_to_real_stage_2_synthetic_epoch()
    {
        var request = Request();
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), admissions);
        var started = await recovery.StartOwnedAsync(request);
        using var previous = Process.GetProcessById(started.ProcessId);
        await recovery.StopAllAsync();
        Assert.True(previous.HasExited);
        Assert.False(admissions.TryGet(request.Dataset, out _));
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, request.Dataset) with
            { DataSource = FeedDataSourceMode.Synthetic, Synthetic = new() { RecordCount = 1_000_000, RecordsPerSecond = 100 } },
            Contracts = request.Manifest!.GetRegistrations()
        };
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        publisher.IsRunning.Returns(true);
        var factory = new DatabentoMarketDataEpochFactory(new DatabentoFeedFactory(), publisher, options);
        await using var api = new DatabentoMarketDataApi(factory, new DatabentoMarketDataApiOptions());

        await api.StartAsync(request.ValueDate);
        var reader = api.GetFuturesLastPriceReader("ES20261218");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!reader.TryGetLastTrade(out _)) await Task.Delay(20, deadline.Token);

        Assert.True(api.IsDatabentoFeedUp());
        Assert.True(api.TryGetLastTickPrice("ES20261218", out _));
        Assert.Empty(recovery.Current);
        await api.StopAsync(request.ValueDate);
        Assert.False(reader.TryGetLastTrade(out _));
        output.WriteLine("Stage3 owned child exited; Stage2 in-process native synthetic feed produced fresh prices and stopped.");
    }

    static DatasetWorkerStartRequest Request(bool helper = false)
    {
        var manifest = new DatasetDesiredSubscriptionRegistry().Set("GLBX.MDP3", ValueDate,
            [new DatabentoContractRegistration
            {
                DomainContractId = "ES20261218", ProviderContractName = "ES20261218", Dataset = "GLBX.MDP3",
                RootSymbol = "ES", AssetTypeId = AssetTypeId.Futures, OnTheRun = true, Rollover = true
            }]);
        return new()
        {
            ExecutablePath = DotNetHost(),
            PrefixArguments = helper
                ? [typeof(QualificationHostMarker).Assembly.Location, "--worker", "true"]
                : [typeof(DatasetWorkerAssemblyMarker).Assembly.Location],
            Dataset = manifest.Dataset,
            ValueDate = manifest.ValueDate,
            GenerationId = Guid.NewGuid(),
            WorkerInstanceId = Guid.NewGuid(),
            Manifest = manifest,
            ManifestRevision = manifest.Revision
        };
    }

    static DatabentoDatasetResetRequest Reset(DatasetWorkerProcessSnapshot current) => new(
        current.Dataset, current.GenerationId, ValueDate,
        DatabentoDatasetFailureReason.NativeDrainStalled, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), Guid.NewGuid());

    static DatabentoStage3Options Options() => new()
    {
        WorkerHandshakeTimeout = TimeSpan.FromSeconds(10),
        WorkerStartTimeout = TimeSpan.FromSeconds(15),
        WorkerCommandTimeout = TimeSpan.FromSeconds(5),
        WorkerGracefulStopTimeout = TimeSpan.FromMilliseconds(300),
        WorkerForceKillTimeout = TimeSpan.FromSeconds(5)
    };

    static string DotNetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var path = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        return File.Exists(path) ? path : throw new InvalidOperationException("dotnet host not found.");
    }

    static async Task TerminateOwnedAsync(Process process)
    {
        if (process.HasExited) return;
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}

public sealed class WindowsQualificationFactAttribute : FactAttribute
{
    public WindowsQualificationFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Windows job-object parent-crash containment requires Windows.";
    }
}

public sealed class LinuxQualificationFactAttribute : FactAttribute
{
    public LinuxQualificationFactAttribute()
    {
        if (!OperatingSystem.IsLinux()) Skip = "SIGTERM-resistant process-group descendants require Linux.";
    }
}
