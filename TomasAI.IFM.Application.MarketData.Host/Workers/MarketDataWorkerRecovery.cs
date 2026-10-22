using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
namespace TomasAI.IFM.Application.MarketData.Host;

public sealed partial class MarketDataWorkerHost
{
    readonly Guid hostInstanceId = Guid.NewGuid();
    long statusRevision;
    readonly Dictionary<string, MarketDataDatasetStatus> roleStates = new(StringComparer.Ordinal);
    readonly Dictionary<string, SemaphoreSlim> recoveryLocks = new(StringComparer.Ordinal);
    public MarketDataReadSnapshot CaptureReadSnapshot()
    {
        lock (gate)
        {
            var values = backendState ?? throw new InvalidOperationException("Backend values are not configured.");
            var active = workers.Where(pair => pair.Key.StartsWith("Futures|", StringComparison.Ordinal))
                .Select(pair => manifests[pair.Key].Manifest).ToArray();
            var contracts = active.SelectMany(manifest => manifest.Contracts)
                .Select(contract => values.Futures.GetCatalog().FindFutures(contract.DomainContractId))
                .Where(contract => contract is not null).Cast<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel>().ToArray();
            return new(GetBackendStatus(), active, contracts, values.Capture());
        }
    }
    public async Task<DatasetWorkerControlFrame> ResetOwnedWorkerAsync(string role, Guid workerId,
        DatasetSubscriptionManifest manifest, CancellationToken token)
    {
        var worker = RequireWorker(role, workerId);
        lock (gate)
            if (!manifests.TryGetValue(role + "|" + manifest.Dataset, out var desired)
                || desired.Manifest.Fingerprint != manifest.Validate().Fingerprint)
                throw new InvalidOperationException("Reset must use the currently owned manifest.");
        var result = await RecoverAsync(new(Guid.NewGuid(), role, manifest.Dataset, workerId,
            worker.Current.GenerationId, manifest.ValueDate, "Requested dataset reset"), token).ConfigureAwait(false);
        if (!result.Recovered) throw new InvalidOperationException(result.Error);
        return (await RequireWorker(role, workerId).GetHealthAsync(token).ConfigureAwait(false)) with { BootstrapToken = "" };
    }
    public MarketDataBackendStatus GetBackendStatus()
    {
        lock (gate) return new(ready, workers.Values.Select(worker => (DateOnly?)manifests.Values.First(
            request => request.WorkerInstanceId == worker.Current.WorkerInstanceId).Manifest.ValueDate).FirstOrDefault(),
            roleStates.Values.ToArray(), hostInstanceId, statusRevision, workers.Values.Select(worker => MarketDataHostConnection.Encode(worker.Current)).ToArray());
    }
    public async Task<MarketDataRecoveryResult> RecoverAsync(MarketDataRecoveryRequest request, CancellationToken token)
    {
        var role = ValidateRole(request.Role);
        var key = role + "|" + request.Dataset;
        MarketDataWorkerStart manifest;
        DatasetWorkerProcessSupervisor worker;
        SemaphoreSlim serial;
        lock (gate)
        {
            if (!manifests.TryGetValue(key, out manifest!) || manifest.Manifest.ValueDate != request.ValueDate
                || !workers.TryGetValue(key, out worker!) || identities[key] != request.ExpectedWorkerInstanceId
                || request.ExpectedGenerationId == Guid.Empty || worker.Current.GenerationId != request.ExpectedGenerationId)
                throw new InvalidOperationException("Recovery target no longer identifies the owned dataset process.");
            if (!recoveryLocks.TryGetValue(key, out serial!)) recoveryLocks.Add(key, serial = new(1, 1));
        }
        if (!await serial.WaitAsync(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false))
            throw new InvalidOperationException("This dataset is already recovering.");
        try
        {
            lock (gate)
                if (!workers.TryGetValue(key, out var current) || !ReferenceEquals(current, worker)
                    || worker.Current.GenerationId != request.ExpectedGenerationId)
                    throw new InvalidOperationException("Recovery target changed before recovery admission.");
            Exception? failure = null;
            var softTimeout = configuration.GetValue("MarketDataHost:SoftRecoveryTimeout", TimeSpan.FromMinutes(2));
            if (softTimeout <= TimeSpan.Zero || softTimeout > TimeSpan.FromMinutes(2))
                throw new InvalidOperationException("Soft recovery timeout must be positive and at most two minutes.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(softTimeout);
            var stage = "reset";
            await PublishStateAsync(role, request.Dataset, "Recovering", request.Reason, worker.Current, token, request.OperationId).ConfigureAwait(false);
            backendState?.Fence(role, request.Dataset, request.ExpectedGenerationId);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (worker.RequiresProcessReplacement)
                        throw new InvalidDataException("Worker connection is unusable; replacing its process and pipes.");
                    var reset = await worker.RunWithRecoveryDeadlineAsync(deadline.Token, async budget =>
                    {
                        stage = "reset/readiness";
                        var response = await worker.ResetAsync(manifest.Manifest, budget).ConfigureAwait(false);
                        if (!response.Healthy) throw new InvalidOperationException(response.Detail);
                        stage = "option lease restoration";
                        await RestoreOptionLeasesAsync(role, request.Dataset, worker, budget).ConfigureAwait(false);
                        return response;
                    }).ConfigureAwait(false);
                    await PublishStateAsync(role, request.Dataset, "Healthy", null, worker.Current, token, request.OperationId).ConfigureAwait(false);
                    return new(request.OperationId, role, request.Dataset, true, true, true, null,
                        reset.WorkerInstanceId, reset.GenerationId);
                }
                catch (Exception error)
                {
                    failure = deadline.IsCancellationRequested && !token.IsCancellationRequested
                        ? new TimeoutException($"Recovery deadline of {softTimeout.TotalSeconds:F3}s expired during {stage}.", error)
                        : error;
                    if (deadline.IsCancellationRequested || token.IsCancellationRequested) break;
                    // A failed read may have consumed part of a frame. Never reset over that pipe again.
                    try
                    {
                        stage = "process replacement";
                        worker = await ReplaceRecoveryWorkerAsync(role, request.Dataset, worker, manifest, deadline.Token).ConfigureAwait(false);
                        stage = "option lease restoration";
                        await worker.RunWithRecoveryDeadlineAsync(deadline.Token, async budget =>
                        {
                            await RestoreOptionLeasesAsync(role, request.Dataset, worker, budget).ConfigureAwait(false);
                            return true;
                        }).ConfigureAwait(false);
                        await PublishStateAsync(role, request.Dataset, "Healthy", null, worker.Current, token, request.OperationId).ConfigureAwait(false);
                        return new(request.OperationId, role, request.Dataset, true, true, true, null,
                            worker.Current.WorkerInstanceId, worker.Current.GenerationId);
                    }
                    catch (Exception replacementError)
                    {
                        failure = deadline.IsCancellationRequested && !token.IsCancellationRequested
                            ? new TimeoutException($"Recovery deadline of {softTimeout.TotalSeconds:F3}s expired during {stage}.", replacementError)
                            : replacementError;
                        if (deadline.IsCancellationRequested || token.IsCancellationRequested) break;
                        lock (gate) worker = workers[key];
                    }
                }
            }
            // Failure is visible immediately at expiry. Cleanup is a separate post-failure operation.
            var terminalReason = $"{role} feed recovery failed; dataset stopped. " + failure?.Message;
            await PublishStateAsync(role, request.Dataset, "CleanupUnconfirmed", terminalReason,
                worker.Current, CancellationToken.None, request.OperationId).ConfigureAwait(false);
            try
            {
                await StopWorkerAsync(role, request.ExpectedWorkerInstanceId).WaitAsync(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
                if (role == "Options" && backendState?.RetireOptions is { } retire)
                {
                    using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await retire(cleanupDeadline.Token).WaitAsync(cleanupDeadline.Token).ConfigureAwait(false);
                }
                var reason = terminalReason;
                await PublishStateAsync(role, request.Dataset, "StoppedError", reason, worker.Current, CancellationToken.None, request.OperationId).ConfigureAwait(false);
                return new(request.OperationId, role, request.Dataset, false, true, false, reason,
                    worker.Current.WorkerInstanceId, worker.Current.GenerationId);
            }
            catch (Exception cleanup)
            {
                var reason = terminalReason + " Dataset cleanup could not be confirmed. " + cleanup.Message;
                await PublishStateAsync(role, request.Dataset, "CleanupUnconfirmed", reason, worker.Current, CancellationToken.None, request.OperationId).ConfigureAwait(false);
                return new(request.OperationId, role, request.Dataset, false, false, false, reason,
                    worker.Current.WorkerInstanceId, worker.Current.GenerationId);
            }
        }
        finally { serial.Release(); }
    }
    async Task PublishStateAsync(string role, string dataset, string lifecycle, string? error,
        DatasetWorkerProcessSnapshot process, CancellationToken token, Guid operationId = default)
    {
        DateOnly? valueDate;
        lock (gate) valueDate = manifests.GetValueOrDefault(role + "|" + dataset)?.Manifest.ValueDate;
        var state = new MarketDataDatasetStatus(role, dataset, process.WorkerInstanceId, process.GenerationId,
            process.ProcessId, process.Running, process.Healthy, lifecycle, error, valueDate, operationId, process.ManifestRevision, DateTimeOffset.UtcNow);
        lock (gate)
        {
            var key = role + "|" + dataset;
            if (identities.TryGetValue(key, out var owner) && owner != process.WorkerInstanceId) return;
            if (!identities.ContainsKey(key) && roleStates.TryGetValue(key, out var previous)
                && previous.WorkerInstanceId != process.WorkerInstanceId) return;
            roleStates[key] = state;
            statusRevision++;
            if (lifecycle == "Healthy" && manifests.TryGetValue(key, out var manifest))
                backendState?.Activate(role, manifest.Manifest, process);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var client = await connection.GetClientAsync(deadline.Token).ConfigureAwait(false);
            await client.PublishAsync(connection.Scope + ".state." + role + "." + dataset, state,
                serializer: NatsMessagePackSerializer<MarketDataDatasetStatus>.Default,
                cancellationToken: deadline.Token).ConfigureAwait(false);
        }
        catch (Exception notification)
        {
            // State remains queryable after reconnect even when the event could not be delivered.
            logger.LogWarning(notification, "Market-data state notification failed; Role={Role}; Dataset={Dataset}", role, dataset);
        }
    }
}
