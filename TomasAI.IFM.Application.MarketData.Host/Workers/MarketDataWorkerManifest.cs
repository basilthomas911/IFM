using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Host;

public sealed partial class MarketDataWorkerHost
{
    public async Task<DatasetWorkerProcessSnapshot> ApplyOptionsManifestAsync(Guid workerId, DatasetSubscriptionManifest manifest, CancellationToken token)
    {
        manifest.Validate();
        var worker = RequireWorker("Options", workerId);
        var key = "Options|" + manifest.Dataset;
        SemaphoreSlim serial;
        lock (gate)
        {
            if (manifests[key].Manifest.Fingerprint == manifest.Fingerprint) return worker.Current;
            if (!recoveryLocks.TryGetValue(key, out serial!)) recoveryLocks.Add(key, serial = new(1, 1));
        }
        await serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (gate)
                if (!identities.TryGetValue(key, out var identity) || identity != workerId)
                    throw new InvalidOperationException("Manifest target was retired.");
            await PublishStateAsync("Options", manifest.Dataset, "Recovering", "Applying committed option underlyings", worker.Current, token).ConfigureAwait(false);
            backendState?.Fence("Options", manifest.Dataset, worker.Current.GenerationId);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var response = await worker.ApplyManifestAsync(manifest, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            if (!response.Healthy) throw new InvalidOperationException(response.Detail);
            lock (gate) manifests[key] = new(manifest, workerId, response.GenerationId);
            await RestoreOptionLeasesAsync("Options", manifest.Dataset, worker, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            await PublishStateAsync("Options", manifest.Dataset, "Healthy", null, worker.Current, token).ConfigureAwait(false);
            return worker.Current;
        }
        catch (Exception error)
        {
            try
            {
                await StopWorkerAsync("Options", workerId).WaitAsync(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
                if (backendState?.RetireOptions is { } retire)
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await retire(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
                }
                await PublishStateAsync("Options", manifest.Dataset, "StoppedError", error.Message, worker.Current, CancellationToken.None, Guid.NewGuid()).ConfigureAwait(false);
            }
            catch (Exception cleanup)
            {
                await PublishStateAsync("Options", manifest.Dataset, "CleanupUnconfirmed", cleanup.Message, worker.Current, CancellationToken.None, Guid.NewGuid()).ConfigureAwait(false);
            }
            throw;
        }
        finally { serial.Release(); }
    }
}
