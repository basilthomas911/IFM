using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetWorkerProcessSupervisorTests
{
    [Fact]
    public async Task Started_worker_reports_realized_native_generation_not_bootstrap_identity()
    {
        await using var supervisor = new DatasetWorkerProcessSupervisor(Options());
        var request = Request(Guid.NewGuid());

        var started = await supervisor.StartAsync(request);

        started.Healthy.Should().BeTrue(started.Detail);
        started.GenerationId.Should().NotBe(request.GenerationId,
            "the native epoch creates the realized generation; bootstrap identity is not data admission");
    }

    [Fact]
    public async Task Worker_handshakes_reports_health_resets_generation_and_stops_gracefully()
    {
        await using var supervisor = new DatasetWorkerProcessSupervisor(Options());
        var original = Guid.NewGuid();

        var started = await supervisor.StartAsync(Request(original));
        var health = await supervisor.GetHealthAsync();
        var reset = await supervisor.ResetAsync();
        var afterReset = await supervisor.GetHealthAsync();
        var stopped = await supervisor.StopAsync();

        started.Running.Should().BeTrue();
        health.Healthy.Should().BeTrue();
        reset.GenerationId.Should().NotBe(original);
        afterReset.GenerationId.Should().Be(reset.GenerationId);
        stopped.Running.Should().BeFalse();
        stopped.GracefulStopSucceeded.Should().BeTrue();
        stopped.ForcedTermination.Should().BeFalse();
        stopped.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Non_cooperative_worker_is_forcibly_terminated_as_an_exact_child_tree()
    {
        await using var supervisor = new DatasetWorkerProcessSupervisor(Options() with
        {
            WorkerCommandTimeout = TimeSpan.FromMilliseconds(200),
            WorkerGracefulStopTimeout = TimeSpan.FromMilliseconds(200),
            WorkerForceKillTimeout = TimeSpan.FromSeconds(5)
        });
        await supervisor.StartAsync(Request(Guid.NewGuid()));
        await supervisor.HangAsync();

        var stopped = await supervisor.StopAsync();

        stopped.Running.Should().BeFalse();
        stopped.ForcedTermination.Should().BeTrue();
        stopped.GracefulStopSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Starting_a_second_worker_on_the_same_supervisor_is_rejected()
    {
        await using var supervisor = new DatasetWorkerProcessSupervisor(Options());
        await supervisor.StartAsync(Request(Guid.NewGuid()));

        var action = () => supervisor.StartAsync(Request(Guid.NewGuid()));

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Worker_process_owns_a_real_synthetic_native_and_managed_dataset_generation()
    {
        await using var supervisor = new DatasetWorkerProcessSupervisor(Options() with
        {
            WorkerHandshakeTimeout = TimeSpan.FromSeconds(15),
            WorkerCommandTimeout = TimeSpan.FromSeconds(15),
            WorkerGracefulStopTimeout = TimeSpan.FromSeconds(10)
        });
        var request = Request(Guid.NewGuid()) with
        {
            PrefixArguments = WorkerArguments()
        };

        await supervisor.StartAsync(request);
        var health = await supervisor.GetHealthAsync();
        var reset = await supervisor.ResetAsync();
        var stopped = await supervisor.StopAsync();

        health.Healthy.Should().BeTrue(health.Detail);
        health.Detail.Should().Contain("aggregation=True");
        reset.Healthy.Should().BeTrue(reset.Detail);
        stopped.GracefulStopSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Recovery_service_closes_old_admission_before_starting_and_admitting_replacement()
    {
        var options = Options();
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var recovery = new DatasetWorkerProcessRecoveryService(options, admissions);
        var original = Guid.NewGuid();
        var request = Request(original);
        var started = await recovery.StartOwnedAsync(request);
        original = started.GenerationId;

        var result = await recovery.ReplaceProcessAsync(new DatabentoDatasetResetRequest(
            request.Dataset, original, request.ValueDate,
            DatabentoDatasetFailureReason.NativeDrainStalled,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), Guid.NewGuid()),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue(result.Detail);
        result.GenerationId.Should().NotBe(original);
        admissions.TryGet(request.Dataset, out var admitted).Should().BeTrue();
        admitted.WorkerInstanceId.Should().NotBe(request.WorkerInstanceId);
        admitted.GenerationId.Should().Be(result.GenerationId);
        recovery.Current.Should().ContainSingle(snapshot => snapshot.Running);
    }

    [Fact]
    public async Task Recovery_service_rejects_stale_replacement_without_disturbing_current_worker()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), admissions);
        var request = Request(Guid.NewGuid());
        var started = await recovery.StartOwnedAsync(request);

        var result = await recovery.ReplaceProcessAsync(new DatabentoDatasetResetRequest(
            request.Dataset, Guid.NewGuid(), request.ValueDate,
            DatabentoDatasetFailureReason.NativeDrainStalled,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), Guid.NewGuid()),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        recovery.Current.Should().ContainSingle(snapshot =>
            snapshot.ProcessId == started.ProcessId && snapshot.Running);
        admissions.TryGet(request.Dataset, out var admitted).Should().BeTrue();
        admitted.GenerationId.Should().Be(started.GenerationId);
    }

    [Fact]
    public async Task Synthetic_native_pipeline_streams_generation_identified_publications_to_host()
    {
        var received = new TaskCompletionSource<DatasetPublicationEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var supervisor = new DatasetWorkerProcessSupervisor(Options(),
            (publication, _) =>
            {
                received.TrySetResult(publication);
                return ValueTask.CompletedTask;
            });
        var request = Request(Guid.NewGuid());

        var started = await supervisor.StartAsync(request);
        var publication = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        publication.Dataset.Should().Be(request.Dataset);
        publication.ValueDate.Should().Be(request.ValueDate);
        publication.WorkerInstanceId.Should().Be(request.WorkerInstanceId);
        publication.GenerationId.Should().Be(started.GenerationId);
        publication.PublicationSequence.Should().BePositive();
        publication.Payload.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Supervised_lifecycle_starts_one_dataset_owner_resets_generation_and_stops_it()
    {
        var registrations = Substitute.For<IDatabentoContractRegistrationRegistry>();
        registrations.Snapshot().Returns([
            new DatabentoContractRegistration
            {
                DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
                AssetTypeId = Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,
                RootSymbol = "ES", Dataset = "GLBX.MDP3", OnTheRun = true, Rollover = true
            }
        ]);
        var authority = Substitute.For<IDatabentoContractAuthority>();
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var workers = new DatasetWorkerProcessRecoveryService(Options(), admissions);
        var runtime = new SupervisedDatabentoLifecycleRuntime(authority, registrations, workers,
            new DatabentoSupervisedWorkerOptions
            {
                DotNetHostPath = DotNetHost(),
                WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location,
                Synthetic = new TomasAI.IFM.Framework.MarketData.DataBento.SyntheticFeedOptions
                {
                    RecordCount = 1_000_000, RecordsPerSecond = 100, StartSequence = 1
                }
            }, TimeProvider.System,
            Substitute.For<TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation.ITickAggregationEventPublisher>());

        await runtime.StartAsync(new DateOnly(2026, 9, 4), CancellationToken.None);
        var before = await runtime.GetWatchdogSnapshotAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        var reset = await runtime.ResetDatasetAsync(new DatabentoDatasetResetRequest(
            "GLBX.MDP3", before.Feeds.Single().GenerationId, new DateOnly(2026, 9, 4),
            DatabentoDatasetFailureReason.NativeDrainStalled, TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5), Guid.NewGuid()), CancellationToken.None);
        await runtime.StopAsync(CancellationToken.None);

        before.Complete.Should().BeTrue(before.FailureDetail);
        before.Feeds.Should().ContainSingle(feed => feed.ProducerAlive && feed.AggregationWorkerRunning);
        reset.Succeeded.Should().BeTrue(reset.Detail);
        reset.GenerationId.Should().NotBe(before.Feeds.Single().GenerationId);
        workers.Current.Should().BeEmpty();
        runtime.ActiveValueDate.Should().BeNull();
    }

    static DatasetWorkerStartRequest Request(Guid generation) => new()
    {
        ExecutablePath = DotNetHost(),
        PrefixArguments = WorkerArguments(),
        Dataset = "GLBX.MDP3",
        ValueDate = new(2026, 9, 4),
        WorkerInstanceId = Guid.NewGuid(),
        GenerationId = generation,
        Manifest = new DatasetDesiredSubscriptionRegistry().Set("GLBX.MDP3", new(2026, 9, 4),
        [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ES20261218",
            AssetTypeId = Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,
            RootSymbol = "ES", Dataset = "GLBX.MDP3", OnTheRun = true, Rollover = true
        }])
    };

    static string[] WorkerArguments() =>
        [typeof(DatasetWorkerAssemblyMarker).Assembly.Location];

    static string DotNetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        var executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var candidate = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, executable);
        if (File.Exists(candidate)) return candidate;
        throw new InvalidOperationException("The test could not resolve the absolute dotnet host path.");
    }

    static DatabentoStage3Options Options() => new()
    {
        WorkerHandshakeTimeout = TimeSpan.FromSeconds(5),
        WorkerCommandTimeout = TimeSpan.FromSeconds(3),
        WorkerGracefulStopTimeout = TimeSpan.FromSeconds(3),
        WorkerForceKillTimeout = TimeSpan.FromSeconds(5)
    };
}
