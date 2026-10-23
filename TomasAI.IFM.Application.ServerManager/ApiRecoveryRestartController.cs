using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager;

/// <summary>External, single-attempt escalation. A durable claim precedes termination; API memory cannot reset it.</summary>
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
                // CreateNew is atomic across managers and all API incarnations. Flush before killing anything.
                using var claim = new FileStream(ledger, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
                await claim.WriteAsync(bytes, token).ConfigureAwait(false); claim.Flush(flushToDisk: true);
            }
            catch (IOException) when (File.Exists(ledger))
            {
                return await SaveAsync(new(request.OperationId, request.ValueDate, "Failed", request.ProcessId, null, now,
                    "The API restart allowance for this value date has already been used. No further automatic restart will occur; reload the trade and enable Live Feed manually."), token).ConfigureAwait(false);
            }
            int? replacement = null;
            using var deadline = new CancellationTokenSource(RestartBudget, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
            try
            {
                log($"API recovery restart started: {request.OperationId}; old PID={request.ProcessId}; budget=120 seconds.");
                var work = Task.Run(() => restart(request, linked.Token), CancellationToken.None);
                _ = work.ContinueWith(task => _ = task.Exception,CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
                replacement = await work.WaitAsync(linked.Token).ConfigureAwait(false);
                if (replacement == request.ProcessId) throw new InvalidOperationException("API process identity did not change.");
                var proof = Task.Run(() => verify(request, replacement.Value, linked.Token),CancellationToken.None);
                _ = proof.ContinueWith(task => _ = task.Exception,CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
                await proof.WaitAsync(linked.Token).ConfigureAwait(false);
                return await SaveAsync(new(request.OperationId, request.ValueDate, "Recovered", request.ProcessId, replacement, clock.GetUtcNow().UtcDateTime, null), token).ConfigureAwait(false);
            }
            catch (Exception error) when (!token.IsCancellationRequested)
            {
                var detail = deadline.IsCancellationRequested ? "The two-minute API restart/restore deadline expired." : error.Message;
                return await SaveAsync(new(request.OperationId, request.ValueDate, "Failed", request.ProcessId, replacement, clock.GetUtcNow().UtcDateTime,
                    "API recovery failed: " + detail + " No further automatic API restart will occur for this value date."), token).ConfigureAwait(false);
            }
        }
        finally { serial.Release(); }
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
                // The allowance is per exchange session. Expired records cannot affect the next value date.
                foreach (var path in Directory.EnumerateFiles(Path.GetDirectoryName(directory)!, "api-restart-*.json").ToArray())
                {
                    try
                    {
                        var claim=JsonSerializer.Deserialize<ApiRecoveryRestartRequest>(await File.ReadAllTextAsync(path,token).ConfigureAwait(false));
                        if (claim is not null && claim.SessionEndUtc <= clock.GetUtcNow().UtcDateTime) File.Delete(path);
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
