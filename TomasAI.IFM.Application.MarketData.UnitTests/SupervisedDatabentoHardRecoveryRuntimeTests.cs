using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using NSubstitute;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class SupervisedDatabentoHardRecoveryRuntimeTests
{
    [Fact]
    public async Task Stale_or_changed_frozen_manifest_fails_before_worker_containment_or_launch()
    {
        var date = new DateOnly(2026, 9, 4);
        var desired = new DatasetDesiredSubscriptionRegistry();
        desired.Set("GLBX.MDP3", date, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        var now = TimeProvider.System.GetUtcNow();
        var stale = new DatasetRecoveryManifestSnapshot(date, now.AddHours(-1),
            desired.CaptureRecoverySet(date, ["GLBX.MDP3"]));
        var current = desired.CaptureRecoverySnapshot(date, ["GLBX.MDP3"], TimeProvider.System);
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), new(),
            desiredSubscriptions: desired);
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers,
            new() { DotNetHostPath = DotNetHost(),
                WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location },
            TimeProvider.System);

        var staleResult = await runtime.AttemptAsync(stale, true, TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
            CancellationToken.None);
        desired.Set("GLBX.MDP3", date, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20270319", ProviderContractName = "ESH7",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        var changedResult = await runtime.AttemptAsync(current, true, TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal("FrozenManifest", staleResult.Stage);
        Assert.Equal("FrozenManifest", changedResult.Stage);
        Assert.False(staleResult.Succeeded);
        Assert.False(changedResult.Succeeded);
        Assert.Empty(workers.Current);
    }

    [Fact]
    public async Task Uncontained_NATS_send_is_unsafe_and_cannot_start_a_new_worker()
    {
        var date = new DateOnly(2026, 9, 4);
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", date, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), new(),
            desiredSubscriptions: desired);
        var diagnostics = Substitute.For<ITickAggregationPublisherDiagnostics>();
        diagnostics.GetSnapshot().Returns(new RealtimeTickPublisherSnapshot(
            true, false, true, false, true, 1, 0, 1, TimeSpan.Zero, TimeSpan.Zero,
            0, 0, 0, 0, 0, 0, 0, 0, RealtimeTickPublisherFailure.NonCooperativeSend, "stuck"));
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers,
            new() { DotNetHostPath = DotNetHost(),
                WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location },
            TimeProvider.System, diagnostics);

        var result = await runtime.AttemptAsync([manifest], true, TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.SafeToRetry);
        Assert.Equal("PublisherIsolation", result.Stage);
        Assert.Empty(workers.Current);
    }

    [Fact]
    public async Task Send_becoming_uncontained_during_worker_containment_cannot_start_a_candidate()
    {
        var date = new DateOnly(2026, 9, 4);
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", date, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), new(),
            desiredSubscriptions: desired);
        var diagnostics = Substitute.For<ITickAggregationPublisherDiagnostics>();
        var safe = new RealtimeTickPublisherSnapshot(true, true, false, true, false,
            1, 0, 1, TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0, 0,
            RealtimeTickPublisherFailure.None, string.Empty);
        diagnostics.GetSnapshot().Returns(safe, safe with
        {
            UncontainedSend = true,
            Failure = RealtimeTickPublisherFailure.NonCooperativeSend
        });
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers,
            new() { DotNetHostPath = DotNetHost(),
                WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location },
            TimeProvider.System, diagnostics);

        var result = await runtime.AttemptAsync([manifest], true, TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.SafeToRetry);
        Assert.Equal("PublisherIsolation", result.Stage);
        Assert.Empty(workers.Current);
        _ = diagnostics.Received(2).GetSnapshot();
    }

    [Fact]
    public async Task Retired_noncooperative_send_still_blocks_new_worker_generation()
    {
        var date = new DateOnly(2026, 9, 4);
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", date, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), new(),
            desiredSubscriptions: desired);
        var diagnostics = Substitute.For<ITickAggregationPublisherDiagnostics>();
        diagnostics.GetSnapshot().Returns(new RealtimeTickPublisherSnapshot(
            true, false, true, false, false, 1, 0, 0, TimeSpan.Zero, TimeSpan.Zero,
            0, 0, 0, 0, 0, 0, 0, 0, RealtimeTickPublisherFailure.NonCooperativeSend,
            "send completed but downstream acceptance is unknown"));
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers,
            new() { DotNetHostPath = DotNetHost(),
                WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location },
            TimeProvider.System, diagnostics);

        var result = await runtime.AttemptAsync([manifest], true, TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.SafeToRetry);
        Assert.Equal("PublisherIsolation", result.Stage);
        Assert.Empty(workers.Current);
    }

    [Fact]
    public async Task Synthetic_hard_attempt_qualifies_Databento_without_admitting_downstream()
    {
        var date = new DateOnly(2026, 9, 4);
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", date, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), admissions,
            desiredSubscriptions: desired);
        var options = new DatabentoSupervisedWorkerOptions
        {
            DotNetHostPath = DotNetHost(),
            WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location,
            DeploymentProfile = FeedDeploymentProfile.SyntheticCi,
            Synthetic = new SyntheticFeedOptions { RecordCount = 100_000, RecordsPerSecond = 50 }
        };
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers, options, TimeProvider.System);

        var result = await runtime.AttemptAsync([manifest], true, TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Single(result.Workers);
        Assert.False(admissions.TryGet(manifest.Dataset, out _));
        var contained = await workers.ContainForHardRecoveryAsync(TimeSpan.FromSeconds(20), CancellationToken.None);
        Assert.True(contained.Isolated, contained.Failure?.ToString());
    }

    [Fact]
    public async Task Failed_first_worker_launch_does_not_poison_the_second_hard_attempt()
    {
        var date = new DateOnly(2026, 9, 4);
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", date, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        var attempts = 0;
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), new(),
            supervisorFactory: options => Interlocked.Increment(ref attempts) == 1
                ? throw new IOException("Injected first launch failure")
                : new DatasetWorkerProcessSupervisor(options), desiredSubscriptions: desired);
        var options = new DatabentoSupervisedWorkerOptions
        {
            DotNetHostPath = DotNetHost(),
            WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location,
            DeploymentProfile = FeedDeploymentProfile.SyntheticCi,
            Synthetic = new SyntheticFeedOptions { RecordCount = 100_000, RecordsPerSecond = 50 }
        };
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers, options, TimeProvider.System);
        var engine = new DatabentoHardRecoveryEngine((_, token) => runtime.AttemptAsync([manifest], true,
            TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), token),
            TimeProvider.System, new()
            {
                AttemptTwoDelay = TimeSpan.Zero,
                AttemptThreeDelay = TimeSpan.Zero
            });

        var result = await engine.ExecuteAsync(new(Guid.NewGuid(), date, Guid.NewGuid(),
            "Test", "First launch failure"), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.DatabentoHealthy, result.Outcome);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, attempts);
    }

    static string DotNetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var candidate = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        return File.Exists(candidate) ? candidate : throw new InvalidOperationException("dotnet host was not found.");
    }
}
