using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.ScheduledTask.Shared;

/// <summary>Checks actual feed readiness before a scheduled market opening reports completion.</summary>
public static class ScheduledMarketOpenReadiness
{
    /// <summary>Rejects invalid or closed sessions and dates awaiting the previous EOD commitment.</summary>
    /// <param name="session">Authoritative market session returned by the API.</param>
    /// <exception cref="InvalidOperationException">The operational date is not valid for opening the feed.</exception>
    public static void ValidateSession(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.MarketSessionReadModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsValid || !session.IsMarketOpen)
            throw new InvalidOperationException("Market open requires a valid open futures session.");
        if (session.IsEndOfDayPending || session.ActiveValueDate != session.OperationalValueDate)
            throw new InvalidOperationException("Market open is waiting for successful EOD completion of the prior operational value date.");
    }

    /// <summary>Requires the admitted date, a live native generation, and all expected GLBX subscriptions.</summary>
    /// <param name="runtime">Current application-owned feed runtime status.</param>
    /// <param name="readiness">Current Databento generation and subscription readiness.</param>
    /// <param name="valueDate">Operational value date admitted for this opening.</param>
    /// <returns>True only when the admitted session has a running and subscribed core feed.</returns>
    public static bool IsReady(MarketDataFeedRuntimeStatusReadModel? runtime, DatabentoReadinessReadModel? readiness, DateOnly valueDate)
        => runtime is { IsRunning: true } && runtime.ActiveValueDate == valueDate &&
            readiness is { CoreReady: true } && readiness.ValueDate == valueDate && readiness.NativeGeneration != Guid.Empty &&
            readiness.Feeds.Any(feed => feed.Dataset == "GLBX.MDP3" && feed.ProducerAlive && feed.AggregationWorkerRunning &&
                feed.ExpectedSubscriptions > 0 && feed.ReceivedSubscriptions >= feed.ExpectedSubscriptions);
}
