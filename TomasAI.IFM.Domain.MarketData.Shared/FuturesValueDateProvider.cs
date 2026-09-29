namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Provides the non-null futures value date using US Eastern session boundaries.</summary>
public sealed class FuturesValueDateProvider(TimeProvider timeProvider) : IValueDateProvider
{
    /// <summary>Gets the process-wide provider backed by the system clock.</summary>
    public static IValueDateProvider System { get; } = new FuturesValueDateProvider(TimeProvider.System);

    readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public DateOnly ValueDate => GetValueDate(_timeProvider.GetUtcNow());

    /// <inheritdoc />
    public DateOnly GetValueDate(DateTimeOffset instant)
        => FuturesTradingValueDate.GetOperational(instant);
}
