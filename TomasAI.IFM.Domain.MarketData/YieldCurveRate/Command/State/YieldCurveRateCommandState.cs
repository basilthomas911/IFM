using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;

/// <summary>
/// Represents the state of yield curve rates within the system, including their creation, modification, and removal.
/// </summary>
/// <remarks>This class manages the lifecycle of yield curve rates by applying domain events that represent state
/// changes. It maintains an internal mapping of yield curve rates by value date, allowing for efficient operations such as checking
/// for existence, adding, updating, and removing rates. The state is updated based on specific domain events, such
/// as <see cref="YieldCurveRateAddedEvent"/>, <see cref="YieldCurveRateChangedEvent"/>, <see cref="YieldCurveRateRemovedEvent"/>, 
/// and <see cref="YieldCurveRatesImportedEvent"/>.</remarks>
public class YieldCurveRateCommandState
    : BaseEventSourceActorState<YieldCurveRateCommandState>, IEventSourceActorState<YieldCurveRateCommandState>
{
    // Command decisions only need existence by value date. Retaining every
    // maturity value duplicates the event payload throughout state replay.
    readonly HashSet<DateOnly> _yieldCurveRateDates = [];

    public override ActorThreadId Id { get; set; }

    /// <summary>
    /// Apply state change event
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                YieldCurveRateAddedEvent e => On(e),
                YieldCurveRateChangedEvent e => On(e),
                YieldCurveRateRemovedEvent e => On(e),
                YieldCurveRatesImportedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Create yield curve rate
    /// </summary>
    /// <param name="e"></param>
    bool On(YieldCurveRateAddedEvent e)
    {
        _yieldCurveRateDates.Add(e.YieldCurveRate.ValueDate);
        return true;
    }

    /// <summary>
    /// Change yield curve rate
    /// </summary>
    /// <param name="e"></param>
    bool On(YieldCurveRateChangedEvent e)
    {
        _yieldCurveRateDates.Add(e.YieldCurveRate.ValueDate);
        return true;
    }

    /// <summary>
    /// Delete yield curve rate
    /// </summary>
    /// <param name="e"></param>
    bool On(YieldCurveRateRemovedEvent e)
    {
        _yieldCurveRateDates.Remove(e.ValueDate);
        return true;
    }

    /// <summary>
    /// Import yield curve rates
    /// </summary>
    /// <param name="e"></param>
    bool On(YieldCurveRatesImportedEvent e)
    {
        _yieldCurveRateDates.EnsureCapacity(_yieldCurveRateDates.Count + e.YieldCurveRates.Length);
        foreach (var yieldCurveRate in e.YieldCurveRates)
            _yieldCurveRateDates.Add(yieldCurveRate.ValueDate);
        return true;
    }

    /// <summary>
    /// Determines whether a yield curve rate exists for the specified value date.
    /// </summary>
    /// <param name="valueDate">The date for which to check the existence of a yield curve rate.</param>
    /// <param name="overwrite">A boolean value indicating whether to consider the rate as existing regardless of its presence in the
    /// collection. If <see langword="true"/>, the method will return <see langword="true"/> even if the rate is not
    /// found.</param>
    /// <returns><see langword="true"/> if a yield curve rate exists for the specified date and <paramref name="overwrite"/> is
    /// <see langword="false"/>;  otherwise, <see langword="false"/>.</returns>
    internal bool YieldCurveRateExists(DateOnly valueDate, bool overwrite)
        => _yieldCurveRateDates.Contains(valueDate) && !overwrite;

    /// <summary>
    /// Determines whether a yield curve rate does not exist for the specified value date.
    /// </summary>
    /// <param name="valueDate">The date for which to check the existence of a yield curve rate.</param>
    /// <param name="overwrite">A boolean value indicating whether to consider overwriting existing data. If <see langword="true"/>, the method
    /// will return <see langword="false"/> regardless of the rate's existence.</param>
    /// <returns><see langword="true"/> if a yield curve rate does not exist for the specified value date and <paramref name="overwrite"/> is
    /// <see langword="false"/>; otherwise, <see langword="false"/>.</returns>
    internal bool YieldCurveRateDoesNotExist(DateOnly valueDate, bool overwrite)
        => !_yieldCurveRateDates.Contains(valueDate) && !overwrite;
}
