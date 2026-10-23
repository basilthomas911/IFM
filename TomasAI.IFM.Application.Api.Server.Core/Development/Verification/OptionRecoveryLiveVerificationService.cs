using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

namespace TomasAI.IFM.Application.Api.Server.Core.Development.Verification;

internal sealed record OptionRecoveryVerificationRequest(Guid OperationId, string Mode, DateTimeOffset ExpiresAtUtc, int DelaySeconds = 300);

/// <summary>Explicitly armed local Development test. Never submits orders or restarts the API.</summary>
public sealed class OptionRecoveryLiveVerificationService(IHostEnvironment environment, IConfiguration configuration,
    IMarketDataApi market, OptionPipelineRecoveryCoordinator recovery,
    TradeRealtimeCacheRegistry caches, ILogger<OptionRecoveryLiveVerificationService> logger) : BackgroundService
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("MarketDataRecovery:DevelopmentVerification:Enabled")) return;
        var root = new DirectoryInfo(environment.ContentRootPath);
        while (root.Parent is not null && !Directory.Exists(Path.Combine(root.FullName, ".git")) && !File.Exists(Path.Combine(root.FullName, ".git"))) root = root.Parent;
        var directory = Path.Combine(root.FullName, ".artifacts", "option-recovery-verification");
        var requestPath = Path.Combine(directory, "request.json");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            Directory.CreateDirectory(directory);
            do
            {
                if (!File.Exists(requestPath)) continue;
                // Consumption precedes injection. An API restart never replays an armed request.
                var claimed = Path.Combine(directory, $"claimed-{Guid.NewGuid():N}.json");
                File.Move(requestPath, claimed);
                Guid operation = default;
                try
                {
                    var request = JsonSerializer.Deserialize<OptionRecoveryVerificationRequest>(await File.ReadAllTextAsync(claimed, stoppingToken), Json)
                        ?? throw new InvalidDataException("Missing verification request.");
                    operation = request.OperationId;
                    Validate(request, DateTimeOffset.UtcNow);
                    await RunAsync(request, directory, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    logger.LogError(error, "Development option recovery verification failed; OperationId={OperationId}", operation);
                    await ReportAsync(directory, operation, "TestError", new { Error = error.Message }, stoppingToken);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            // Local test-file failures must never stop the trading API.
            logger.LogError(error, "Development recovery verifier stopped; API processing remains running. No further verification requests will be consumed until restart.");
        }
    }

    internal static void Validate(OptionRecoveryVerificationRequest request, DateTimeOffset now)
    {
        if (request.OperationId == Guid.Empty || request.Mode is not ("Happy" or "Deadline")
            || request.DelaySeconds is < 300 or > 3600 || request.ExpiresAtUtc <= now || request.ExpiresAtUtc > now.AddHours(2))
            throw new InvalidDataException("Verification requires a new operation ID, Happy/Deadline mode, a 300-3600 second delay and an unexpired request (maximum two hours).");
    }

    async Task RunAsync(OptionRecoveryVerificationRequest request, string directory, CancellationToken token)
    {
        await WaitForInjectionAsync(request, TimeProvider.System,
            (elapsed, remaining) => ReportAsync(directory, request.OperationId, "ArmedCountdown", new
                { request.Mode, ElapsedSeconds = elapsed.TotalSeconds, RemainingSeconds = remaining.TotalSeconds, request.DelaySeconds }, token), token).ConfigureAwait(false);
        // Read starting conditions only after the fixed countdown. Stale values are evidence, never an arming gate.
        var backend = await ReadStatusAsync(token).ConfigureAwait(false);
        var options = backend.Datasets.Single(dataset => dataset.Role == "Options" && dataset.Dataset == "GLBX.MDP3");
        var inspection = CaptureTargets(caches, options.ValueDate ?? throw new InvalidOperationException("Options worker has no active value date."));
        var futures = backend.Datasets.Where(dataset => dataset.Role == "Futures").ToArray();
        var injectedAt = DateTimeOffset.UtcNow;
        await ReportAsync(directory, request.OperationId, "Injecting", new { request.Mode, InjectedAtUtc = injectedAt, OptionsBefore = options, FuturesBefore = futures }, token);
        using (var injection = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            injection.CancelAfter(TimeSpan.FromSeconds(10));
            if (!await market.InjectDevelopmentOptionWorkerFaultAsync(new(request.OperationId, options.WorkerInstanceId, options.GenerationId, inspection.ValueDate!.Value), injection.Token).WaitAsync(injection.Token))
                throw new InvalidOperationException("Host did not acknowledge fault injection.");
        }
        recovery.BeginDevelopmentVerification(inspection with { Failure = "Explicitly armed Development Options-worker hang: " + request.Mode }, request.OperationId, request.Mode == "Deadline", token);
        var elapsed = Stopwatch.StartNew();
        var completion = recovery.Completion!;
        await ReportAsync(directory, request.OperationId, "Recovering", new { request.Mode, InjectedAtUtc = injectedAt, OptionsBefore = options, FuturesBefore = futures }, token);
        await completion.WaitAsync(token).ConfigureAwait(false);
        var after = await ReadStatusAsync(token).ConfigureAwait(false);
        var unchanged = futures.All(before => after.Datasets.Any(current => current.Role == "Futures" && current.Dataset == before.Dataset
            && current.ProcessId == before.ProcessId && current.GenerationId == before.GenerationId && current.Running && current.Healthy));
        var expected = request.Mode == "Happy" ? recovery.State == "Healthy" : recovery.State == "Failed" && elapsed.Elapsed >= TimeSpan.FromSeconds(119);
        await ReportAsync(directory, request.OperationId, expected && unchanged ? "Passed" : "Failed", new
        {
            request.Mode, InjectedAtUtc = injectedAt, RecoveryElapsedSeconds = elapsed.Elapsed.TotalSeconds, RecoveryState = recovery.State,
            FuturesUnchanged = unchanged, OptionsBefore = options, FuturesBefore = futures, After = after.Datasets,
            Error = recovery.Failure?.ErrorMessage,
            UiDialogRequiresHumanConfirmation = request.Mode == "Deadline"
        }, token);
        logger.LogInformation("Development option recovery verification finished; OperationId={OperationId}; Mode={Mode}; State={State}; FuturesUnchanged={FuturesUnchanged}", request.OperationId, request.Mode, recovery.State, unchanged);
    }

    internal static async Task WaitForInjectionAsync(OptionRecoveryVerificationRequest request, TimeProvider clock,
        Func<TimeSpan, TimeSpan, Task> report, CancellationToken token)
    {
        var start = clock.GetTimestamp();
        var delay = TimeSpan.FromSeconds(request.DelaySeconds);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (clock.GetUtcNow() >= request.ExpiresAtUtc) throw new TimeoutException("Verification request expired; no fault was injected.");
            var elapsed = clock.GetElapsedTime(start);
            var remaining = delay - elapsed;
            if (remaining <= TimeSpan.Zero) return;
            await report(elapsed, remaining).ConfigureAwait(false);
            await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), clock, token).ConfigureAwait(false);
        }
    }

    internal static OptionPipelineInspection CaptureTargets(TradeRealtimeCacheRegistry caches, DateOnly date)
        => new(date, "Explicit Development fault; starting state may already be degraded.", caches.CaptureRecoveryObservations()
            .Where(trade => trade.ValueDate == date && trade.Position?.IsOpen == true)
            .Select(trade => trade.PositionId.Format()).ToArray());

    async Task<MarketDataBackendStatus> ReadStatusAsync(CancellationToken token)
    {
        using var io = CancellationTokenSource.CreateLinkedTokenSource(token);
        io.CancelAfter(TimeSpan.FromSeconds(10));
        return await market.GetBackendStatusAsync(io.Token).WaitAsync(io.Token).ConfigureAwait(false);
    }

    async Task ReportAsync(string directory, Guid operation, string state, object detail, CancellationToken token)
    {
        if (!await TryWriteReportAsync(directory, operation, state, detail, token).ConfigureAwait(false))
            logger.LogWarning("Development verification report is temporarily unavailable; test continues; OperationId={OperationId}; State={State}", operation, state);
    }

    internal static async Task<bool> TryWriteReportAsync(string directory, Guid operation, string state, object detail, CancellationToken token)
    {
        try
        {
            var report = JsonSerializer.Serialize(new { OperationId = operation, State = state, AtUtc = DateTimeOffset.UtcNow, Detail = detail }, Json);
            var target = Path.Combine(directory, operation.ToString("D") + ".json");
            var temporary = target + ".tmp";
            await File.WriteAllTextAsync(temporary, report, token);
            File.Move(temporary, target, overwrite: true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
