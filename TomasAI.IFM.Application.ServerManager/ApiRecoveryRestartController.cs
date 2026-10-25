using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager;

/// <summary>External escalation. A durable claim blocks another restart until end-to-end recovery is verified.</summary>
public sealed class ApiRecoveryRestartController(string directory, string sessionId,
    Func<ApiRecoveryRestartRequest, CancellationToken, Task<int>> restart,
    Func<ApiRecoveryRestartRequest, int, CancellationToken, Task> verify,
    Func<ApiRecoveryRestartResult, CancellationToken, Task> notify,
    Action<string> log, TimeProvider? timeProvider = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly SemaphoreSlim serial = new(1, 1);
    public static TimeSpan RestartBudget => TimeSpan.FromMinutes(2);

    public async Task<ApiRecoveryRestartResult> HandleAsync(ApiRecoveryRestartRequest request, CancellationToken token)
    {
        await serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);
            var resultPath = ApiRecoveryRestartProtocol.ResultPath(directory, request.OperationId);
            if (File.Exists(resultPath)) return JsonSerializer.Deserialize<ApiRecoveryRestartResult>(await File.ReadAllTextAsync(resultPath, token).ConfigureAwait(false))!;
            var now = clock.GetUtcNow().UtcDateTime;
            var rejection = request.SessionId != sessionId || request.ProcessId <= 0 || request.OperationId == Guid.Empty
                || request.TradeIds.Length == 0 || request.ValueDate == default || request.RequestedUtc > now.AddSeconds(5)
                || now - request.RequestedUtc > RestartBudget || request.SessionEndUtc <= now
                ? "Stale, expired or unowned API restart request; no process was stopped." : null;
            if (rejection is not null) return await SaveAsync(new(request.OperationId, request.ValueDate, "Rejected", request.ProcessId, null, now, rejection), token).ConfigureAwait(false);
            // A fatal early recovery error still cannot initiate API reset before the initial recovery window expires.
            if (request.RecoveryDeadlineUtc > now)
            {
                if (request.RecoveryDeadlineUtc - now > RestartBudget + TimeSpan.FromSeconds(5)) throw new InvalidDataException("Invalid initial recovery deadline.");
                await Task.Delay(request.RecoveryDeadlineUtc - now, clock, token).ConfigureAwait(false);
            }
            if (request.SessionEndUtc <= clock.GetUtcNow().UtcDateTime)
                return await SaveAsync(new(request.OperationId,request.ValueDate,"Rejected",request.ProcessId,null,clock.GetUtcNow().UtcDateTime,"The market session closed before API restart; no process was stopped."),token).ConfigureAwait(false);
            var ledger = Path.Combine(Path.GetDirectoryName(directory)!, $"api-restart-{request.ValueDate:yyyy-MM-dd}.json");
            try
            {
                using var ledgerLock = await LockLedgerAsync(ledger, token).ConfigureAwait(false);
                await ReleaseVerifiedClaimCoreAsync(ledger, token).ConfigureAwait(false);
                // CreateNew is atomic across managers and all API incarnations. Flush before killing anything.
                using var claim = new FileStream(ledger, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
                await claim.WriteAsync(bytes, token).ConfigureAwait(false); claim.Flush(flushToDisk: true);
            }
            catch (IOException) when (File.Exists(ledger))
            {
                return await SaveAsync(new(request.OperationId, request.ValueDate, "Failed", request.ProcessId, null, now,
                    "An API restart for this value date is still outstanding or failed. Another automatic restart is blocked until recovery is verified."), token).ConfigureAwait(false);
            }
            int? replacement = null;
            string? lastError = null;
            using var deadline = new CancellationTokenSource(RestartBudget, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
            async Task<T> RetryAsync<T>(Func<Task<T>> action, string stage)
            {
                for (var attempt = 1; ; attempt++)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var work = Task.Run(action, CancellationToken.None);
                    _ = work.ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    try { return await work.WaitAsync(linked.Token).ConfigureAwait(false); }
                    catch (Exception error) when (!linked.IsCancellationRequested)
                    {
                        lastError = stage + ": " + error.Message;
                        log($"API recovery step failed; retrying: {request.OperationId}; stage={stage}; attempt={attempt}; error={error.Message}");
                        await Task.Delay(TimeSpan.FromMilliseconds(250), clock, linked.Token).ConfigureAwait(false);
                    }
                }
            }
            try
            {
                log($"API recovery restart started: {request.OperationId}; old PID={request.ProcessId}; budget=120 seconds.");
                replacement = await RetryAsync(async () => {
                    var pid = await restart(request, linked.Token).ConfigureAwait(false);
                    if (pid <= 0 || pid == request.ProcessId) throw new InvalidOperationException("API process identity did not change.");
                    return pid;
                }, "API restart").ConfigureAwait(false);
                await RetryAsync(async () => { await verify(request, replacement.Value, linked.Token).ConfigureAwait(false); return true; },
                    "Trade/feed verification").ConfigureAwait(false);
                var recovered = await SaveAsync(new(request.OperationId, request.ValueDate, "Recovered", request.ProcessId, replacement, clock.GetUtcNow().UtcDateTime, null), token).ConfigureAwait(false);
                await ReleaseVerifiedClaimAsync(ledger, token).ConfigureAwait(false);
                return recovered;
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                var detail = deadline.IsCancellationRequested ? "The two-minute API restart/restore deadline expired." : error.Message;
                if (lastError is not null) detail += " Last failed step: " + lastError;
                return await SaveAsync(new(request.OperationId, request.ValueDate, "Failed", request.ProcessId, replacement, clock.GetUtcNow().UtcDateTime,
                    "API recovery failed: " + detail + " No further automatic API restart will occur for this value date."), token).ConfigureAwait(false);
            }
        }
        finally { serial.Release(); }
    }

    // Persisted verification is the only authority to release a claim, including after a manager crash.
    // Duplicate delivery remains idempotent because completed per-operation results are retained.
    async Task ReleaseVerifiedClaimAsync(string ledger, CancellationToken token)
    {
        using var ledgerLock = await LockLedgerAsync(ledger, token).ConfigureAwait(false);
        await ReleaseVerifiedClaimCoreAsync(ledger, token).ConfigureAwait(false);
    }

    static async Task<FileStream> LockLedgerAsync(string ledger, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(RestartBudget);
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try { return new FileStream(ledger + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(50, deadline.Token).ConfigureAwait(false); }
        }
    }

    async Task ReleaseVerifiedClaimCoreAsync(string ledger, CancellationToken token)
    {
        if (!File.Exists(ledger)) return;
        var claim = JsonSerializer.Deserialize<ApiRecoveryRestartRequest>(await File.ReadAllTextAsync(ledger, token).ConfigureAwait(false));
        if (claim is null) return;
        var parent = Path.GetDirectoryName(directory)!;
        // Session IDs are protocol identities, never arbitrary filesystem paths.
        if (claim.SessionId.Length != 32 || !claim.SessionId.All(Uri.IsHexDigit))
        {
            // Test/embedded controllers may use a non-GUID session name; only the current session is allowed.
            if (claim.SessionId != sessionId) return;
        }
        var resultPath = claim.SessionId == sessionId ? ApiRecoveryRestartProtocol.ResultPath(directory, claim.OperationId)
            : ApiRecoveryRestartProtocol.ResultPath(Path.Combine(parent, claim.SessionId), claim.OperationId);
        if (!File.Exists(resultPath)) return;
        var proof = JsonSerializer.Deserialize<ApiRecoveryRestartResult>(await File.ReadAllTextAsync(resultPath, token).ConfigureAwait(false));
        if (proof?.OperationId != claim.OperationId || proof.ValueDate != claim.ValueDate || proof.PreviousProcessId != claim.ProcessId || proof.State != "Recovered"
            || proof.Error is not null || proof.ReplacementProcessId is null || proof.ReplacementProcessId == claim.ProcessId) return;
        File.Delete(ledger);
        log($"API recovery verified; restart claim released: {claim.OperationId}; value date={claim.ValueDate}.");
    }

    async Task<ApiRecoveryRestartResult> SaveAsync(ApiRecoveryRestartResult result, CancellationToken token)
    {
        var path = ApiRecoveryRestartProtocol.ResultPath(directory, result.OperationId);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(result), token).ConfigureAwait(false);
        File.Move(temporary, path, true);
        log($"API recovery result: {result.State}; OperationId={result.OperationId}; PID={result.ReplacementProcessId}; {result.Error}");
        return result;
    }

    public async Task RunAsync(CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var delivered = new System.Collections.Generic.HashSet<Guid>();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                // Failed/outstanding claims expire with the exchange session; verified claims release immediately.
                foreach (var path in Directory.EnumerateFiles(Path.GetDirectoryName(directory)!, "api-restart-*.json").ToArray())
                {
                    try
                    {
                        var claim=JsonSerializer.Deserialize<ApiRecoveryRestartRequest>(await File.ReadAllTextAsync(path,token).ConfigureAwait(false));
                        if (claim is not null && claim.SessionEndUtc <= clock.GetUtcNow().UtcDateTime) File.Delete(path);
                        else await ReleaseVerifiedClaimAsync(path, token).ConfigureAwait(false);
                    }
                    catch (IOException) { }
                    catch (JsonException) { } // An incomplete claim remains used; never grant a second restart after uncertain persistence.
                }
                foreach (var path in Directory.EnumerateFiles(directory, "request-*.json").ToArray())
                {
                    try
                    {
                        var request = JsonSerializer.Deserialize<ApiRecoveryRestartRequest>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false))!;
                        await HandleAsync(request, token).ConfigureAwait(false);
                        File.Delete(path);
                    }
                    catch (Exception error) when (!token.IsCancellationRequested) { log("API restart request retained for retry: " + error.Message); }
                }
                foreach (var path in Directory.EnumerateFiles(directory, "result-*.json").ToArray())
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<ApiRecoveryRestartResult>(await File.ReadAllTextAsync(path, token).ConfigureAwait(false))!;
                        if (result.Error is null || delivered.Contains(result.OperationId)) continue;
                        using var delivery = CancellationTokenSource.CreateLinkedTokenSource(token); delivery.CancelAfter(TimeSpan.FromSeconds(3));
                        await notify(result, delivery.Token).WaitAsync(delivery.Token).ConfigureAwait(false);
                        delivered.Add(result.OperationId);
                    }
                    catch (Exception error) when (!token.IsCancellationRequested) { log("API recovery UI notification retained for retry: " + error.Message); }
                }
            } while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
