using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.MarketClose;

namespace TomasAI.IFM.Domain.SystemAdmin.MarketClose;

/// <summary>Serializes session close, pins its API-owned date, and persists EOD before maintenance.</summary>
public sealed class MarketCloseCoordinator(IFuturesMarketSessionAuthority sessions, IMarketCloseOperations operations,
    TimeProvider time, ILogger<MarketCloseCoordinator> logger)
{
    readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Runs the parameterless close operation. Cancellation is owned by the API host.</summary>
    public async Task<MarketCloseResult> CloseMarketAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        Guid runId = Guid.Empty;
        try
        {
            var session = sessions.Current;
            var now = time.GetUtcNow().UtcDateTime;
            var date = FuturesTradingValueDate.GetOperational(new DateTimeOffset(now));
            if (!session.IsValid) throw new InvalidOperationException("The API market session is not initialized.");
            var completed = await operations.GetCompletedDateAsync(token).ConfigureAwait(false);
            if (completed is { } prior && prior >= date)
            {
                // Every close stops ingestion, including a feed restarted after financial EOD.
                await operations.StopFeedsAsync(date, token).ConfigureAwait(false);
                await operations.PurgeCompletedMessagesAsync(token).ConfigureAwait(false);
                logger.LogInformation("MarketClose AlreadyFinalized ValueDate={ValueDate}", date);
                return new(date, Guid.Empty, 0, true);
            }
            var previousBoundary = await operations.GetPreviousCloseBoundaryAsync(date, token).ConfigureAwait(false);
            var scheduledBoundary = FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime;
            var boundary = previousBoundary ?? (now < scheduledBoundary ? now : scheduledBoundary);
            logger.LogInformation("MarketClose Starting ValueDate={ValueDate} Boundary={Boundary} Intraday={Intraday}", date, boundary, boundary < scheduledBoundary);
            runId = await operations.BeginAsync(date, boundary, token).ConfigureAwait(false);
            await operations.StopFeedsAsync(date, token).ConfigureAwait(false);
            await operations.RecordStageAsync(runId, date, "FeedsStopped", "Correlated feed-stop completion confirmed.", token).ConfigureAwait(false);
            logger.LogInformation("MarketClose FeedsStopped RunId={RunId} ValueDate={ValueDate}", runId, date);
            byte[]? cursor = null;
            var finalized = new HashSet<Guid>();
            do
            {
                var page = await operations.LoadPositionsAsync(date, cursor, token).ConfigureAwait(false);
                foreach (var position in page.Positions)
                {
                    if (!finalized.Add(position.Id.PositionId)) continue;
                    if (position.LatestSealedValueDate < date)
                        await operations.FinalizePositionAsync(position, date, boundary, token).ConfigureAwait(false);
                    logger.LogInformation("MarketClose PositionFinalized RunId={RunId} PositionId={PositionId} ValueDate={ValueDate}", runId, position.Id.Format(), date);
                }
                cursor = page.PagingState;
            } while (cursor is { Length: > 0 });
            await operations.RecordStageAsync(runId, date, "PositionsFinalized", $"All {finalized.Count} positions finalized; EOD source commitments confirmed.", token).ConfigureAwait(false);
            logger.LogInformation("MarketClose EndOfDayCommitted RunId={RunId} ValueDate={ValueDate} Positions={Positions}", runId, date, finalized.Count);
            await operations.PurgeCompletedMessagesAsync(token).ConfigureAwait(false);
            await operations.CompleteAsync(runId, token).ConfigureAwait(false);
            logger.LogInformation("MarketClose Completed RunId={RunId} ValueDate={ValueDate}", runId, date);
            return new(date, runId, finalized.Count, false);
        }
        catch (Exception error)
        {
            logger.LogError(error, "MarketClose Failed RunId={RunId}", runId);
            if (runId != Guid.Empty)
            {
                try
                {
                    using var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await operations.FailAsync(runId, error, receiptDeadline.Token).ConfigureAwait(false);
                }
                catch (Exception receiptError) { logger.LogError(receiptError, "MarketClose FailureReceiptFailed RunId={RunId}", runId); }
            }
            throw;
        }
        finally { gate.Release(); }
    }
}
