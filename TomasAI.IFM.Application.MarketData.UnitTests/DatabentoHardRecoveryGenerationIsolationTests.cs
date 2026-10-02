using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoHardRecoveryGenerationIsolationTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 30);

    [Fact]
    public void Delayed_old_pipe_callback_cannot_reenter_after_replacement_is_admitted()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var original = Admission();
        admissions.Admit(original);
        Assert.True(admissions.TryAccept(original, 1, out var queuedTick));

        admissions.Close(original.Dataset, original.GenerationId);
        var replacement = Admission();
        admissions.Admit(replacement);

        Assert.True(queuedTick.IsCancellationRequested);
        Assert.False(admissions.TryAccept(original, 2));
        Assert.False(admissions.TryAccept(original, 3));
        Assert.True(admissions.TryAccept(replacement, 1, out var currentTick));
        Assert.False(currentTick.IsCancellationRequested);
        Assert.True(admissions.RejectedPublications >= 2);
        admissions.Close(replacement.Dataset, replacement.GenerationId);
    }

    [Fact]
    public async Task Three_clean_synthetic_attempts_use_distinct_worker_processes_and_identities()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", ValueDate, [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", RootSymbol = "ES",
            OnTheRun = true, Rollover = true
        }]);
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var workers = new DatasetWorkerProcessRecoveryService(new()
        {
            WorkerHandshakeTimeout = TimeSpan.FromSeconds(10),
            WorkerStartTimeout = TimeSpan.FromSeconds(15),
            WorkerCommandTimeout = TimeSpan.FromSeconds(5),
            WorkerGracefulStopTimeout = TimeSpan.FromMilliseconds(300),
            WorkerForceKillTimeout = TimeSpan.FromSeconds(5)
        }, admissions, desiredSubscriptions: desired);
        var snapshots = new List<DatasetWorkerProcessSnapshot>();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var request = new DatasetWorkerStartRequest
            {
                ExecutablePath = DotNetHost(),
                PrefixArguments = [typeof(DatasetWorkerAssemblyMarker).Assembly.Location],
                Dataset = manifest.Dataset,
                ValueDate = manifest.ValueDate,
                WorkerInstanceId = Guid.NewGuid(),
                GenerationId = Guid.NewGuid(),
                Manifest = manifest,
                ManifestRevision = manifest.Revision
            };
            var started = await workers.StartCandidateAsync(request);
            snapshots.Add(started);
            Assert.True(started.Running);
            Assert.True(started.ProcessId > 0);
            Assert.False(admissions.TryGet(manifest.Dataset, out _));

            var containment = await workers.ContainForHardRecoveryAsync(
                TimeSpan.FromSeconds(20), CancellationToken.None);
            Assert.True(containment.Isolated, containment.Failure?.ToString());
            Assert.Contains(started.ProcessId, containment.ProcessIds);
            Assert.Empty(workers.Current);
        }

        Assert.Equal(3, snapshots.Select(worker => worker.ProcessId).Distinct().Count());
        Assert.Equal(3, snapshots.Select(worker => worker.WorkerInstanceId).Distinct().Count());
        Assert.Equal(3, snapshots.Select(worker => worker.GenerationId).Distinct().Count());
    }

    static DatasetWorkerAdmission Admission() =>
        new("GLBX.MDP3", ValueDate, Guid.NewGuid(), Guid.NewGuid(), 1);

    static string DotNetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var candidate = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        return File.Exists(candidate) ? candidate : throw new InvalidOperationException("dotnet host was not found.");
    }
}
