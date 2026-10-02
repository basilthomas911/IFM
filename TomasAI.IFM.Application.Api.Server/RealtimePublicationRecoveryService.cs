using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Enforces the fast safety boundary between the unpausable Databento workers and the
/// downstream NATS publisher. The broader live-pipeline audit remains minute based.
/// </summary>
public sealed class RealtimePublicationRecoveryService(
    ITickAggregationEventPublisher publisher,
    IFuturesMarketSessionAuthority sessions,
    DatabentoMarketDataWatchdogService watchdog,
    TimeProvider time,
    ILogger<RealtimePublicationRecoveryService> logger,
    IDatabentoRecoveryRequester? recoveryRequester = null) : BackgroundService
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    static readonly TimeSpan FailedResetRetryDelay = TimeSpan.FromSeconds(5);
    DateTime? lastAttemptUtc;
    DateTime? attemptedFailureUtc;
    DateTime? episodeFailureUtc;
    bool episodeSubmittedForFailure;
    bool terminalEpisode;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ObserveAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception,
                    "Realtime publication recovery observation failed; the service will continue polling.");
            }
            await Task.Delay(PollInterval, time, stoppingToken).ConfigureAwait(false);
        }
    }

    async Task ObserveAsync(CancellationToken stoppingToken)
    {
        if (terminalEpisode) return;
        if (publisher is not ITickAggregationPublisherDiagnostics diagnostics)
            return;
        var session = sessions.Current;
        if (session.ActiveValueDate is not { } valueDate)
        {
            attemptedFailureUtc = null;
            lastAttemptUtc = null;
            episodeFailureUtc = null;
            episodeSubmittedForFailure = false;
            return;
        }
        var snapshot = diagnostics.GetSnapshot();
        if (!snapshot.PolicyEnabled || !snapshot.ResetRequired)
        {
            episodeFailureUtc = null;
            episodeSubmittedForFailure = false;
            return;
        }
        if (episodeSubmittedForFailure && episodeFailureUtc == snapshot.FirstFailureUtc)
            return;
        var now = time.GetUtcNow().UtcDateTime;
        if (attemptedFailureUtc == snapshot.FirstFailureUtc
            && lastAttemptUtc is { } attempted
            && now - attempted < FailedResetRetryDelay)
            return;

        var correlationId = Guid.CreateVersion7(time.GetUtcNow());
        attemptedFailureUtc = snapshot.FirstFailureUtc;
        lastAttemptUtc = now;
        logger.LogCritical(
            "Realtime NATS publication made no progress for {NoProgressMs} ms. Resetting the complete Databento dataset. CorrelationId={CorrelationId}; Failure={Failure}; FirstFailureUtc={FirstFailureUtc}; EventType={EventType}; Subject={Subject}; Attempt={Attempt}; Depth={Depth}; InFlight={InFlight}; ExceptionType={ExceptionType}; Exception={ExceptionMessage}",
            snapshot.NoProgressAge.TotalMilliseconds, correlationId, snapshot.Failure,
            snapshot.FirstFailureUtc, snapshot.InFlightEventType, snapshot.InFlightSubject,
            snapshot.CurrentAttempt, snapshot.Depth, snapshot.InFlight,
            snapshot.LastExceptionType, snapshot.LastExceptionMessage);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(1));
            episodeFailureUtc = snapshot.FirstFailureUtc;
            episodeSubmittedForFailure = true;
            logger.LogWarning("Requesting hard reset recovery after publication stall. CorrelationId={CorrelationId}; ValueDate={ValueDate}", correlationId, valueDate);
            var episode = await (recoveryRequester ?? throw new InvalidOperationException("Hard reset recovery pipeline is not configured."))
                .HardResetRecoveryAsync(new DatabentoHardRecoveryRequest(correlationId, valueDate,
                    watchdog.Current.NativeGeneration, nameof(RealtimePublicationRecoveryService),
                    $"Downstream publication stalled: {snapshot.Failure}"), deadline.Token)
                .ConfigureAwait(false);
            terminalEpisode = episode.Outcome is DatabentoRecoveryRequestOutcome.Unrecoverable
                or DatabentoRecoveryRequestOutcome.ApplicationStopping;
            if (episode.Outcome == DatabentoRecoveryRequestOutcome.AlreadyInProgress)
                return;
            if (episode.Outcome == DatabentoRecoveryRequestOutcome.FullyHealthy)
                logger.LogWarning("Databento recovery episode fully qualified after publication stall. CorrelationId={CorrelationId}; ValueDate={ValueDate}",
                    episode.CorrelationId, valueDate);
            else
                logger.LogCritical("Databento recovery episode did not restore the complete pipeline. CorrelationId={CorrelationId}; ValueDate={ValueDate}; Outcome={Outcome}; Detail={Detail}",
                    episode.CorrelationId, valueDate, episode.Outcome, episode.Detail);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogCritical(exception,
                "Complete Databento dataset reset failed after the downstream publication stall. CorrelationId={CorrelationId}; ValueDate={ValueDate}",
                correlationId, valueDate);
        }
    }
}
