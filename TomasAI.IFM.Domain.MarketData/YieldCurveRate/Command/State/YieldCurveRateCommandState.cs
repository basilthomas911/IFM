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
        switch (domainEvent)
        {
            case YieldCurveRateAddedEvent added when added.YieldCurveRate is not null:
                _yieldCurveRateDates.Add(added.YieldCurveRate.ValueDate);
                return true;
            case YieldCurveRateChangedEvent changed when changed.YieldCurveRate is not null:
                _yieldCurveRateDates.Add(changed.YieldCurveRate.ValueDate);
                return true;
            case YieldCurveRateRemovedEvent removed:
                _yieldCurveRateDates.Remove(removed.ValueDate);
                return true;
            case YieldCurveRatesImportedEvent:
                // Import is an operation marker; external records are projected separately.
                return true;
            default:
                return false;
        }
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
