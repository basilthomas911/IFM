namespace TomasAI.IFM.Domain.MarketData.Shared;
/// <summary>Reconciles the operational date on every read, retaining completed EOD advancement.</summary>
public sealed class FuturesValueDateProvider : IValueDateProvider, ICompletedFuturesEndOfDayProjection, IMarketOpenValueDateInitializer
{
    private readonly System.Threading.Lock _gate = new();
    private readonly TimeProvider _timeProvider;
    private DateOnly _operationalValueDate;
    private DateOnly _lastCompletedValueDate;
    /// <summary>Initializes the operational date from the injected server clock.</summary>
    /// <param name="timeProvider">The API clock checked on every current-value-date read.</param>
    public FuturesValueDateProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _operationalValueDate = FuturesTradingValueDate.GetOperational(timeProvider.GetUtcNow());
    }
    /// <summary>Gets a first-use provider for callers outside the API composition root.</summary>
    public static IValueDateProvider System { get; } = new FuturesValueDateProvider(TimeProvider.System);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1721", Justification = "The read-model property is serialized data; the method implements an existing conversion/provider contract.")]
    public DateOnly ValueDate => GetValueDate(_timeProvider.GetUtcNow());
    /// <inheritdoc />
    public DateOnly GetValueDate(DateTimeOffset instant)
    {
        lock (_gate) return RefreshOperationalDate(instant);
    }
    /// <inheritdoc />
    public DateOnly InitializeMarketOpen() => ValueDate;
    /// <inheritdoc />
    public bool ApplyCompletedEndOfDay(DateOnly completedValueDate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(completedValueDate, default);
        lock (_gate)
        {
            if (completedValueDate <= _lastCompletedValueDate) return false;
            RefreshOperationalDate(_timeProvider.GetUtcNow());
            _lastCompletedValueDate = completedValueDate;
            var next = FuturesTradingValueDate.GetNextTradingDate(completedValueDate);
            if (next > _operationalValueDate) _operationalValueDate = next;
            return true;
        }
    }

    /// <summary>Moves forward with session boundaries; old projections or clock corrections cannot rewind the date.</summary>
    private DateOnly RefreshOperationalDate(DateTimeOffset instant)
    {
        var current = FuturesTradingValueDate.GetOperational(instant);
        if (current > _operationalValueDate) _operationalValueDate = current;
        return _operationalValueDate;
    }
}
