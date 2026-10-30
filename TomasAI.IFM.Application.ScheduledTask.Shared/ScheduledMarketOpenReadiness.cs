using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.ScheduledTask.Shared;

/// <summary>Checks actual feed readiness before a scheduled market opening reports completion.</summary>
public static class ScheduledMarketOpenReadiness
{
    /// <summary>Requires the API-selected date and a live generation. Rust proves
    /// dataset transport/publication readiness internally; process workers retain
    /// their explicit expected/received subscription-counter checks.</summary>
    /// <param name="runtime">Current application-owned feed runtime status.</param>
    /// <param name="readiness">Current Databento generation and subscription readiness.</param>
    /// <param name="valueDate">Operational value date API-selected for this opening.</param>
    /// <returns>True only when the API-selected session has a running and subscribed core feed.</returns>
    public static bool IsReady(MarketDataFeedRuntimeStatusReadModel? runtime, DatabentoReadinessReadModel? readiness, DateOnly valueDate)
        => runtime is { IsRunning: true } && runtime.ActiveValueDate == valueDate &&
            readiness is { CoreReady: true } && readiness.ValueDate == valueDate && readiness.NativeGeneration != Guid.Empty &&
            readiness.Feeds.Any(feed => feed.Dataset == "GLBX.MDP3" && feed.ProducerAlive && feed.AggregationWorkerRunning &&
                (readiness.NativeDatasetOwnership
                    ? feed.FeedKind == "Futures" && feed.NativeState == "Healthy" && feed.MajorStatus == "Healthy"
                    : feed.ExpectedSubscriptions > 0 && feed.ReceivedSubscriptions >= feed.ExpectedSubscriptions));
}
