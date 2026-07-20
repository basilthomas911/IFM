using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Model;

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
    readonly Dictionary<DateOnly, YieldCurveRateModel> _yieldCurveRates = [];

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
        if (_yieldCurveRates.ContainsKey(e.YieldCurveRate.ValueDate))
            _yieldCurveRates.Remove(e.YieldCurveRate.ValueDate);
        _yieldCurveRates.Add(e.YieldCurveRate.ValueDate, new YieldCurveRateModel(e.YieldCurveRate));
        return true;
    }

    /// <summary>
    /// Change yield curve rate
    /// </summary>
    /// <param name="e"></param>
    bool On(YieldCurveRateChangedEvent e)
    {
        if (_yieldCurveRates.ContainsKey(e.YieldCurveRate.ValueDate))
            _yieldCurveRates.Remove(e.YieldCurveRate.ValueDate);
        _yieldCurveRates.Add(e.YieldCurveRate.ValueDate, new YieldCurveRateModel(e.YieldCurveRate));
        return true;
    }

    /// <summary>
    /// Delete yield curve rate
    /// </summary>
    /// <param name="e"></param>
    bool On(YieldCurveRateRemovedEvent e)
    {
        if (_yieldCurveRates.ContainsKey(e.ValueDate))
            _yieldCurveRates.Remove(e.ValueDate);
        return true;
    }

    /// <summary>
    /// Import yield curve rates
    /// </summary>
    /// <param name="e"></param>
    bool On(YieldCurveRatesImportedEvent e)
    {
        foreach (var yieldCurveRate in e.YieldCurveRates)
        {
            if (_yieldCurveRates.ContainsKey(yieldCurveRate.ValueDate))
                _yieldCurveRates.Remove(yieldCurveRate.ValueDate);
            _yieldCurveRates.Add(yieldCurveRate.ValueDate, new YieldCurveRateModel (yieldCurveRate));
        }
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
        => _yieldCurveRates.ContainsKey(valueDate) && !overwrite;

    /// <summary>
    /// Determines whether a yield curve rate does not exist for the specified value date.
    /// </summary>
    /// <param name="valueDate">The date for which to check the existence of a yield curve rate.</param>
    /// <param name="overwrite">A boolean value indicating whether to consider overwriting existing data. If <see langword="true"/>, the method
    /// will return <see langword="false"/> regardless of the rate's existence.</param>
    /// <returns><see langword="true"/> if a yield curve rate does not exist for the specified value date and <paramref name="overwrite"/> is
    /// <see langword="false"/>; otherwise, <see langword="false"/>.</returns>
    internal bool YieldCurveRateDoesNotExist(DateOnly valueDate, bool overwrite)
        => !_yieldCurveRates.ContainsKey(valueDate) && !overwrite;
}
