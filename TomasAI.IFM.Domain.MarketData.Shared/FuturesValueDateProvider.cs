namespace TomasAI.IFM.Domain.MarketData.Shared;
/// <summary>Projects the operational value date; clock transitions cannot advance an initialized date.</summary>
public sealed class FuturesValueDateProvider : IValueDateProvider, ICompletedFuturesEndOfDayProjection
{
    private readonly System.Threading.Lock _gate = new();
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1721", Justification = "The read-model property is serialized data; the method implements an existing conversion/provider contract.")]
    public DateOnly ValueDate { get { lock (_gate) return _operationalValueDate; } }
    /// <inheritdoc />
    public DateOnly GetValueDate(DateTimeOffset instant) => ValueDate;
    /// <inheritdoc />
    public bool ApplyCompletedEndOfDay(DateOnly completedValueDate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(completedValueDate, default);
        lock (_gate)
        {
            if (completedValueDate <= _lastCompletedValueDate) return false;
            _lastCompletedValueDate = completedValueDate;
            _operationalValueDate = FuturesTradingValueDate.GetNextTradingDate(completedValueDate);
            return true;
        }
    }
}
