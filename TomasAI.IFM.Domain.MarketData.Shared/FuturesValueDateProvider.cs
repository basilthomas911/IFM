namespace TomasAI.IFM.Domain.MarketData.Shared;
/// <summary>Projects the operational value date; clock transitions cannot advance an initialized date.</summary>
public sealed class FuturesValueDateProvider : IValueDateProvider, ICompletedFuturesEndOfDayProjection
{
    private readonly object _gate = new();
    private DateOnly _operationalValueDate;
    private DateOnly _lastCompletedValueDate;
    /// <summary>Initializes a first-use date; persisted completion replaces this bootstrap value before live startup.</summary>
    /// <param name="timeProvider">The clock used only for first-use initialization.</param>
    public FuturesValueDateProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _operationalValueDate = FuturesTradingValueDate.GetOperational(timeProvider.GetUtcNow());
    }
    /// <summary>Gets a first-use provider for callers outside the API composition root.</summary>
    public static IValueDateProvider System { get; } = new FuturesValueDateProvider(TimeProvider.System);
    /// <inheritdoc />
    public DateOnly ValueDate { get { lock (_gate) return _operationalValueDate; } }
    /// <inheritdoc />
    public DateOnly GetValueDate(DateTimeOffset instant) => ValueDate;
    /// <inheritdoc />
    public bool ApplyCompletedEndOfDay(DateOnly completedValueDate)
    {
        if (completedValueDate == default) throw new ArgumentOutOfRangeException(nameof(completedValueDate));
        lock (_gate)
        {
            if (completedValueDate <= _lastCompletedValueDate) return false;
            _lastCompletedValueDate = completedValueDate;
            _operationalValueDate = FuturesTradingValueDate.GetNextTradingDate(completedValueDate);
            return true;
        }
    }
}
/// <summary>Applies only persisted, successful whole-session EOD completion observations to the operational date projection.</summary>
public interface ICompletedFuturesEndOfDayProjection
{
    /// <summary>Advances after successful EOD; older or duplicate completion observations have no effect.</summary>
    /// <param name="completedValueDate">The exchange date whose position EOD commands all succeeded.</param>
    /// <returns>True when a newer persisted completion was applied.</returns>
    bool ApplyCompletedEndOfDay(DateOnly completedValueDate);
}
