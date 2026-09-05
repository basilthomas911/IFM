using System.Diagnostics;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Real synthetic-native child processes, not mocked manifest acknowledgments.</summary>
public sealed class DatasetWorkerManifestIntegrationTests
{
    static readonly DateOnly Date = new(2026, 9, 4);

    [Fact]
    public async Task Worker_applies_complete_revision_once_and_rejects_old_manifest()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var first = desired.Set("GLBX.MDP3", Date, [Registration("ES20261218", "GLBX.MDP3")]);
        await using var worker = new DatasetWorkerProcessSupervisor(Options());
        var started = await worker.StartAsync(Request(first));
        var next = desired.Set(first.Dataset, Date,
            [Registration("ES20261218", first.Dataset), Registration("ES20270319", first.Dataset, false)]);

        var changed = await worker.ApplyManifestAsync(next);
        var duplicate = await worker.ApplyManifestAsync(next);

        Assert.Equal(next.Revision, changed.ManifestRevision);
        Assert.Equal(next.Fingerprint, changed.ManifestFingerprint);
        Assert.NotEqual(started.GenerationId, changed.GenerationId);
        Assert.Equal(changed.GenerationId, duplicate.GenerationId);
        Assert.Contains("contracts=2", changed.Detail);
        await Assert.ThrowsAsync<InvalidDataException>(() => worker.ApplyManifestAsync(first));
        var afterRejection = await worker.GetHealthAsync();
        Assert.True(afterRejection.Healthy, afterRejection.Detail);
        Assert.Equal(next.Revision, afterRejection.ManifestRevision);
        Assert.Equal(changed.GenerationId, afterRejection.GenerationId);
    }

    [Fact]
    public async Task Reset_and_forced_replacement_restore_latest_manifest_and_preserve_other_dataset_readers()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var admissions = new DatasetWorkerAdmissionRegistry();
        using var values = new DatasetWorkerCurrentValues();
        var epochFactory = Substitute.For<IDatabentoMarketDataEpochFactory>();
        await using var api = new DatabentoMarketDataApi(epochFactory, new(), currentValues: values);
        var ingress = new DatasetPublicationIngress(admissions,
            Substitute.For<ITickAggregationEventPublisher>(), Substitute.For<IMarketDataOperationsRecorder>(), values);
        var children = new Dictionary<string, DatasetWorkerProcessSupervisor>();
        var allChildren = new List<DatasetWorkerProcessSupervisor>();
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), admissions, ingress,
            supervisorFactory: options =>
            {
                var child = new DatasetWorkerProcessSupervisor(options,
                    async (message, token) => { await ingress.AcceptAsync(message, token); });
                allChildren.Add(child);
                return child;
            }, desiredSubscriptions: desired, currentValues: values);
        var es = desired.Set("GLBX.MDP3", Date, [Registration("ES20261218", "GLBX.MDP3")]);
        var vx = desired.Set("XCBF.PITCH", Date, [Registration("VX20260916", "XCBF.PITCH")]);
        var first = await recovery.StartOwnedAsync(Request(es));
        children[es.Dataset] = allChildren[^1];
        var unaffected = await recovery.StartOwnedAsync(Request(vx));
        var esReader = api.GetFuturesLastPriceReader("ES20261218");
        var vxReader = api.GetFuturesLastPriceReader("VX20260916");
        await UntilAsync(() => esReader.TryGetLastTrade(out _) && vxReader.TryGetLastTrade(out _));
        Assert.True(api.TryGetLastTickPrice("ES20261218", out _));
        Assert.True(vxReader.TryGetLastTrade(out var vxBefore));

        var next = desired.Set(es.Dataset, Date,
            [Registration("ES20261218", es.Dataset), Registration("ES20270319", es.Dataset, false)]);
        var reset = await recovery.ResetOwnedAsync(Reset(first), CancellationToken.None);
        Assert.True(reset.Succeeded, reset.Detail);
        Assert.True(admissions.TryGet(es.Dataset, out var current));
        Assert.Equal(next.Revision, current.ManifestRevision);
        Assert.Same(esReader, values.GetFuturesReader("ES20261218"));
        Assert.Same(vxReader, values.GetFuturesReader("VX20260916"));
        await UntilAsync(() => esReader.TryGetLastTrade(out _) && values.TryGetLastTickPrice("ES20270319", out _));

        await children[es.Dataset].HangAsync();
        var beforeReplacement = recovery.Current.Single(item => item.Dataset == es.Dataset);
        var replacement = await recovery.ReplaceProcessAsync(Reset(beforeReplacement), CancellationToken.None);
        Assert.True(replacement.Succeeded, replacement.Detail);
        var replaced = recovery.Current.Single(item => item.Dataset == es.Dataset);
        Assert.NotEqual(first.ProcessId, replaced.ProcessId);
        Assert.Equal(next.Revision, replaced.ManifestRevision);
        Assert.Equal(next.Fingerprint, replaced.ManifestFingerprint);
        Assert.False(children[es.Dataset].Current.Running);
        Assert.True(children[es.Dataset].Current.ForcedTermination);
        Assert.Equal(unaffected.ProcessId, recovery.Current.Single(item => item.Dataset == vx.Dataset).ProcessId);
        Assert.Equal(unaffected.GenerationId, recovery.Current.Single(item => item.Dataset == vx.Dataset).GenerationId);
        Assert.True(vxReader.TryGetLastTrade(out _));
        await UntilAsync(() => vxReader.TryGetLastTrade(out var advancing) && advancing.SourceSequence > vxBefore.SourceSequence);
        await UntilAsync(() => esReader.TryGetLastTrade(out _));
        Assert.Same(esReader, api.GetFuturesLastPriceReader("ES20261218"));
        Assert.True(api.TryGetLastTickPrice("ES20261218", out _));
        epochFactory.DidNotReceiveWithAnyArgs().Create(default);

        await recovery.StopAllAsync();
        Assert.False(esReader.TryGetLastTrade(out _));
        Assert.False(vxReader.TryGetLastTrade(out _));
        Assert.Null(values.ActiveValueDate);
    }

    [Fact]
    public async Task Unexpected_child_exit_clears_host_values_and_retained_readers()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var admissions = new DatasetWorkerAdmissionRegistry();
        using var values = new DatasetWorkerCurrentValues();
        var ingress = new DatasetPublicationIngress(admissions,
            Substitute.For<ITickAggregationEventPublisher>(), Substitute.For<IMarketDataOperationsRecorder>(), values);
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), admissions, ingress,
            desiredSubscriptions: desired, currentValues: values);
        var manifest = desired.Set("GLBX.MDP3", Date, [Registration("ES20261218", "GLBX.MDP3")]);
        var started = await recovery.StartOwnedAsync(Request(manifest));
        var reader = values.GetFuturesReader("ES20261218");
        await UntilAsync(() => reader.TryGetLastTrade(out _));
        using var ownedChild = Process.GetProcessById(started.ProcessId);
        ownedChild.Kill(entireProcessTree: true);
        await ownedChild.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await UntilAsync(() => !admissions.TryGet(manifest.Dataset, out _) && !reader.TryGetLastTrade(out _));
        Assert.False(values.IsFeedUp);
    }

    [Fact]
    public async Task Startup_requires_resolved_parent_manifest_before_launching_process()
    {
        await using var worker = new DatasetWorkerProcessSupervisor(Options());
        var manifest = new DatasetDesiredSubscriptionRegistry().Set("GLBX.MDP3", Date,
            [Registration("ES20261218", "GLBX.MDP3")]);
        await Assert.ThrowsAsync<ArgumentException>(() => worker.StartAsync(Request(manifest) with { Manifest = null }));
        Assert.False(worker.Current.Running);
        Assert.Equal(0, worker.Current.ProcessId);
    }

    [Fact]
    public async Task Replacement_queued_behind_shutdown_cannot_start_an_untracked_child()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var children = new List<DatasetWorkerProcessSupervisor>();
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), new(),
            supervisorFactory: options =>
            {
                var child = new DatasetWorkerProcessSupervisor(options);
                children.Add(child);
                return child;
            }, desiredSubscriptions: desired);
        var manifest = desired.Set("GLBX.MDP3", Date, [Registration("ES20261218", "GLBX.MDP3")]);
        var started = await recovery.StartOwnedAsync(Request(manifest));
        await children[0].HangAsync();

        var stop = recovery.StopAllAsync();
        var queued = recovery.ReplaceProcessAsync(Reset(started), CancellationToken.None);
        await stop;
        var result = await queued;

        Assert.False(result.Succeeded);
        Assert.Single(children);
        Assert.Empty(recovery.Current);
        Assert.False(children[0].Current.Running);
    }

    [Fact]
    public async Task Cancellation_after_shutdown_begins_does_not_abandon_later_datasets()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var children = new List<DatasetWorkerProcessSupervisor>();
        await using var recovery = new DatasetWorkerProcessRecoveryService(Options(), new(),
            supervisorFactory: options =>
            {
                var child = new DatasetWorkerProcessSupervisor(options);
                children.Add(child);
                return child;
            }, desiredSubscriptions: desired);
        await recovery.StartOwnedAsync(Request(desired.Set("GLBX.MDP3", Date,
            [Registration("ES20261218", "GLBX.MDP3")])));
        await recovery.StartOwnedAsync(Request(desired.Set("XCBF.PITCH", Date,
            [Registration("VX20260916", "XCBF.PITCH")])));
        await children[0].HangAsync();
        using var cancellation = new CancellationTokenSource();

        var stop = recovery.StopAllAsync(cancellation.Token);
        cancellation.Cancel();
        await stop;

        Assert.Empty(recovery.Current);
        Assert.All(children, child => Assert.False(child.Current.Running));
    }

    static DatabentoDatasetResetRequest Reset(DatasetWorkerProcessSnapshot value) => new(
        value.Dataset, value.GenerationId, Date, DatabentoDatasetFailureReason.NativeDrainStalled,
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), Guid.NewGuid());

    static DatabentoContractRegistration Registration(string id, string dataset, bool front = true) => new()
    {
        DomainContractId = id, ProviderContractName = id, AssetTypeId = AssetTypeId.Futures,
        RootSymbol = id.StartsWith("ES", StringComparison.Ordinal) ? "ES" : "VX",
        Dataset = dataset, OnTheRun = front, Rollover = true
    };

    static DatasetWorkerStartRequest Request(DatasetSubscriptionManifest manifest) => new()
    {
        ExecutablePath = DotNetHost(),
        PrefixArguments = [typeof(DatasetWorkerAssemblyMarker).Assembly.Location],
        Dataset = manifest.Dataset, ValueDate = manifest.ValueDate, GenerationId = Guid.NewGuid(),
        WorkerInstanceId = Guid.NewGuid(), Manifest = manifest, ManifestRevision = manifest.Revision
    };

    static DatabentoStage3Options Options() => new()
    {
        WorkerHandshakeTimeout = TimeSpan.FromSeconds(10), WorkerStartTimeout = TimeSpan.FromSeconds(15),
        WorkerCommandTimeout = TimeSpan.FromSeconds(5), WorkerGracefulStopTimeout = TimeSpan.FromMilliseconds(300),
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

    static async Task UntilAsync(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!predicate()) await Task.Delay(20, deadline.Token);
    }
}
