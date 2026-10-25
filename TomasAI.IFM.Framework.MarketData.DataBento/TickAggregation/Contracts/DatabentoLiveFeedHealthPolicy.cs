using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

/// <summary>
/// Authoritative 5/15-minute policy evaluated from accepted hot-cache mutations.
/// Source age participates so an old backlog cannot make a route appear current.
/// </summary>
public static class DatabentoLiveFeedHealthPolicy
{
    public static readonly TimeSpan GreenLimit = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan YellowLimit = TimeSpan.FromMinutes(15);

    /// <summary>Evaluates the supplied observations against the applicable feed health or qualification criteria.</summary>
    /// <param name="streamActive">Whether the contract stream is activated.</param>
    /// <param name="streamActivatedAtUtc">The UTC instant when the stream was activated.</param>
    /// <param name="lastAcceptedCacheUpdateAtUtc">The UTC time of the latest accepted cache update.</param>
    /// <param name="lastAcceptedSourceEventAtUtc">The source event time of the latest accepted update.</param>
    /// <param name="utcNow">The current UTC instant used to evaluate freshness.</param>
    /// <returns>The evaluate result.</returns>
    public static DatabentoLiveFeedHealthState Evaluate(
        bool streamActive,
        DateTimeOffset? streamActivatedAtUtc,
        DateTimeOffset? lastAcceptedCacheUpdateAtUtc,
        DateTimeOffset? lastAcceptedSourceEventAtUtc,
        DateTimeOffset utcNow)
    {
        if (!streamActive)
            return DatabentoLiveFeedHealthState.Inactive;

        var acceptedAge = Age(utcNow, lastAcceptedCacheUpdateAtUtc ?? streamActivatedAtUtc);
        var sourceAge = lastAcceptedSourceEventAtUtc is null
            ? acceptedAge
            : Age(utcNow, lastAcceptedSourceEventAtUtc);
        var effectiveAge = acceptedAge >= sourceAge ? acceptedAge : sourceAge;
        if (effectiveAge <= GreenLimit)
            return DatabentoLiveFeedHealthState.Green;
        if (effectiveAge <= YellowLimit)
            return DatabentoLiveFeedHealthState.Yellow;
        return DatabentoLiveFeedHealthState.Red;
    }

    static TimeSpan Age(DateTimeOffset utcNow, DateTimeOffset? timestamp) =>
        (timestamp is null) switch { true => TimeSpan.MaxValue, false => (utcNow <= timestamp.Value) switch { true => TimeSpan.Zero, false => utcNow - timestamp.Value } };
}
