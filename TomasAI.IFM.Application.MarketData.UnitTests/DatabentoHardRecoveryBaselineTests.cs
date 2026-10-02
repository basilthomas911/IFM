using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Retains an actual synthetic child-process replacement timing baseline for DHR-00.</summary>
public sealed class DatabentoHardRecoveryBaselineTests
{
    [Fact]
    public async Task Successful_synthetic_worker_replacement_records_elapsed_time_and_exact_process_ownership()
    {
        var valueDate = FuturesTradingValueDate.GetOperational(DateTimeOffset.UtcNow);
        var desired = new DatasetDesiredSubscriptionRegistry();
        var admissions = new DatasetWorkerAdmissionRegistry();
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), admissions,
            desiredSubscriptions: desired);
        var manifest = desired.Set("GLBX.MDP3", valueDate, [Registration()]);
        var started = await recovery.StartOwnedAsync(Request(manifest));
        Assert.True(started.Running);
        Assert.True(started.ProcessId > 0);

        var clock = Stopwatch.StartNew();
        var result = await recovery.ReplaceProcessAsync(new(
            manifest.Dataset, started.GenerationId, valueDate,
            DatabentoDatasetFailureReason.NativeDrainStalled,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), Guid.NewGuid()), CancellationToken.None);
        clock.Stop();

        Assert.True(result.Succeeded, result.Detail);
        var replaced = Assert.Single(recovery.Current);
        Assert.True(replaced.Running);
        Assert.NotEqual(started.ProcessId, replaced.ProcessId);
        Assert.NotEqual(started.GenerationId, replaced.GenerationId);
        Assert.Equal(manifest.Revision, replaced.ManifestRevision);
        Assert.True(admissions.TryGet(manifest.Dataset, out var admitted));
        Assert.Equal(replaced.GenerationId, admitted.GenerationId);
        Console.WriteLine($"DHR-00 synthetic worker replacement: {clock.Elapsed.TotalMilliseconds:F1} ms; old PID={started.ProcessId}; new PID={replaced.ProcessId}; old generation={started.GenerationId:D}; new generation={replaced.GenerationId:D}");

        await recovery.StopAllAsync();
        Assert.Empty(recovery.Current);
        Assert.False(admissions.TryGet(manifest.Dataset, out _));
    }

    static DatabentoContractRegistration Registration() => new()
    {
        DomainContractId = "ES20261218",
        ProviderContractName = "ES20261218",
        AssetTypeId = AssetTypeId.Futures,
        RootSymbol = "ES",
        Dataset = "GLBX.MDP3",
        OnTheRun = true,
        Rollover = true
    };

    static DatasetWorkerStartRequest Request(DatasetSubscriptionManifest manifest) => new()
    {
        ExecutablePath = DotNetHost(),
        PrefixArguments = [typeof(DatasetWorkerAssemblyMarker).Assembly.Location],
        Dataset = manifest.Dataset,
        ValueDate = manifest.ValueDate,
        GenerationId = Guid.NewGuid(),
        WorkerInstanceId = Guid.NewGuid(),
        Manifest = manifest,
        ManifestRevision = manifest.Revision
    };

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
        var candidate = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        return File.Exists(candidate) ? candidate : throw new InvalidOperationException("dotnet host was not found.");
    }
}
