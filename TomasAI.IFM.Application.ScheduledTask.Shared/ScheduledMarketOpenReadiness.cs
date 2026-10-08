using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.ScheduledTask.Shared;

/// <summary>Checks actual feed readiness before a scheduled market opening reports completion.</summary>
public static class ScheduledMarketOpenReadiness
{
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
