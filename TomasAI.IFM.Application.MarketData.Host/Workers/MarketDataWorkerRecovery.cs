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
            var values = backendState;
            var active = workers.Where(pair => pair.Key.StartsWith("Futures|", StringComparison.Ordinal))
                .Select(pair => manifests[pair.Key].Manifest).ToArray();
            var contracts = active.SelectMany(manifest => manifest.Contracts)
                .Select(contract => values?.Futures.GetCatalog().FindFutures(contract.DomainContractId))
                .Where(contract => contract is not null).Cast<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel>().ToArray();
            var status = GetBackendStatus();
            var options = publications.Where(pair => pair.Key.StartsWith("Options|", StringComparison.Ordinal))
                .SelectMany(pair => pair.Value.CaptureObservations())
                .Where(frame => status.Datasets.Any(dataset => dataset.Role == "Options" && dataset.Lifecycle == "Healthy"
                    && dataset.WorkerInstanceId == frame.WorkerInstanceId && dataset.GenerationId == frame.GenerationId
                    && dataset.ValueDate == frame.ValueDate && dataset.ManifestRevision == frame.ManifestRevision)).ToArray();
            return new(status, active, contracts, values?.Capture() ?? [], OptionObservations: options);
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
    public void ReportOptionProcessingFailure(string component, Exception error)
    {
        lock (gate)
            if (roleStates.Values.FirstOrDefault(s => s.Role == "Options" && s.ValueDate.HasValue) is { } option)
                backendState?.ProcessingHealth?.Report(option.ValueDate!.Value, option.GenerationId, component, error);
    }

    public MarketDataBackendStatus GetBackendStatus()
    {
        lock (gate)
        {
            var states = roleStates.Values.ToArray();
            var option = states.FirstOrDefault(state => state.Role == "Options" && state.ValueDate.HasValue);
            var failure = option is null ? null : backendState?.ProcessingHealth?.Read(option.ValueDate!.Value, option.GenerationId);
            if (failure is not null) states = states.Select(state => state.Role == "Options"
                ? state with { Healthy = false, Error = failure.Component + ": " + failure.Code + ": " + failure.Detail } : state).ToArray();
            return new(ready, workers.Values.Select(worker => (DateOnly?)manifests.Values.First(
                request => request.WorkerInstanceId == worker.Current.WorkerInstanceId).Manifest.ValueDate).FirstOrDefault(),
                states, hostInstanceId, statusRevision, workers.Values.Select(worker => MarketDataHostConnection.Encode(worker.Current)).ToArray(),
                failure, option is null ? [] : backendState?.EnabledTrades?.Invoke(option.ValueDate!.Value));
        }
    }
    public async Task<MarketDataRecoveryResult> RecoverAsync(MarketDataRecoveryRequest request, CancellationToken token)
    {
        var role = ValidateRole(request.Role);
        var key = role + "|" + request.Dataset;
        MarketDataWorkerStart manifest;
        DatasetWorkerProcessSupervisor worker;
        SemaphoreSlim serial;
        DateTimeOffset? joinedDeadline = null;
        var joining = false;
        lock (gate)
        {
            if (role == "Options" && roleStates.TryGetValue(key, out var owned) && owned.Lifecycle == "Recovering")
            { joining = true; joinedDeadline = owned.RecoveryDeadlineUtc; }
            if (!manifests.TryGetValue(key, out manifest!) || manifest.Manifest.ValueDate != request.ValueDate
                || !workers.TryGetValue(key, out worker!) || identities[key] != request.ExpectedWorkerInstanceId
                || request.ExpectedGenerationId == Guid.Empty || !joining && worker.Current.GenerationId != request.ExpectedGenerationId)
                throw new InvalidOperationException("Recovery target no longer identifies the owned dataset process.");
            if (!recoveryLocks.TryGetValue(key, out serial!)) recoveryLocks.Add(key, serial = new(1, 1));
        }
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(token);
        var admissionDeadline = joinedDeadline is { } ownedDeadline && (request.DeadlineUtc is null || ownedDeadline < request.DeadlineUtc)
            ? ownedDeadline : request.DeadlineUtc;
        admission.CancelAfter(admissionDeadline is { } requestedDeadline
            ? TimeSpan.FromMilliseconds(Math.Max(1, (requestedDeadline - DateTimeOffset.UtcNow).TotalMilliseconds)) : TimeSpan.FromMinutes(2));
        await serial.WaitAsync(admission.Token).ConfigureAwait(false);
        try
        {
            lock (gate)
            {
                if (role == "Options" && roleStates.TryGetValue(key, out var joined) && joined.Lifecycle == "Healthy"
                    && (joining || joined.GenerationId != request.ExpectedGenerationId) && workers.TryGetValue(key, out var joinedWorker))
                    return new(request.OperationId, role, request.Dataset, true, true, true, null,
                        joinedWorker.Current.WorkerInstanceId, joinedWorker.Current.GenerationId);
                if (!workers.TryGetValue(key, out var current) || !ReferenceEquals(current, worker)
                    || worker.Current.GenerationId != request.ExpectedGenerationId)
                    throw new InvalidOperationException("Recovery target changed before recovery admission.");
            }
            Exception? failure = null;
            var softTimeout = configuration.GetValue("MarketDataHost:SoftRecoveryTimeout", TimeSpan.FromMinutes(2));
            if (softTimeout <= TimeSpan.Zero || softTimeout > TimeSpan.FromMinutes(2))
                throw new InvalidOperationException("Soft recovery timeout must be positive and at most two minutes.");
            if (request.DeadlineUtc is { } outerDeadline)
            {
                var remaining = outerDeadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException("Option pipeline recovery deadline already expired.");
                if (remaining < softTimeout) softTimeout = remaining;
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(softTimeout);
            var stage = "reset";
            await PublishStateAsync(role, request.Dataset, "Recovering", request.Reason, worker.Current, token, request.OperationId, DateTimeOffset.UtcNow + softTimeout).ConfigureAwait(false);
            backendState?.Fence(role, request.Dataset, request.ExpectedGenerationId);
            for (var attempt = 0; role == "Options" || attempt < 3; attempt++)
            {
                try
                {
                    if (request.ReplaceProcessImmediately || worker.RequiresProcessReplacement)
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
                    deadline.Token.ThrowIfCancellationRequested();
                    await PublishStateAsync(role, request.Dataset, "Healthy", null, worker.Current, deadline.Token, request.OperationId).ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
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
                        deadline.Token.ThrowIfCancellationRequested();
                        await PublishStateAsync(role, request.Dataset, "Healthy", null, worker.Current, deadline.Token, request.OperationId).ConfigureAwait(false);
                        deadline.Token.ThrowIfCancellationRequested();
                        return new(request.OperationId, role, request.Dataset, true, true, true, null,
                            worker.Current.WorkerInstanceId, worker.Current.GenerationId);
                    }
                    catch (Exception replacementError)
                    {
                        failure = deadline.IsCancellationRequested && !token.IsCancellationRequested
                            ? new TimeoutException($"Recovery deadline of {softTimeout.TotalSeconds:F3}s expired during {stage}.", replacementError)
                            : replacementError;
                        if (deadline.IsCancellationRequested || token.IsCancellationRequested) break;
                        logger.LogWarning(replacementError, "Dataset recovery attempt failed; Role={Role}; Dataset={Dataset}; OperationId={OperationId}; Attempt={Attempt}; Stage={Stage}; RemainingMilliseconds={RemainingMilliseconds}",
                            role, request.Dataset, request.OperationId, attempt + 1, stage,
                            request.DeadlineUtc is { } end ? Math.Max(0, (end - DateTimeOffset.UtcNow).TotalMilliseconds) : softTimeout.TotalMilliseconds);
                        try { await Task.Delay(250, deadline.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { break; }
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
        DatasetWorkerProcessSnapshot process, CancellationToken token, Guid operationId = default, DateTimeOffset? recoveryDeadlineUtc = null)
    {
        DateOnly? valueDate;
        lock (gate) valueDate = manifests.GetValueOrDefault(role + "|" + dataset)?.Manifest.ValueDate;
        var state = new MarketDataDatasetStatus(role, dataset, process.WorkerInstanceId, process.GenerationId,
            process.ProcessId, process.Running, process.Healthy, lifecycle, error, valueDate, operationId, process.ManifestRevision, DateTimeOffset.UtcNow, recoveryDeadlineUtc);
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
