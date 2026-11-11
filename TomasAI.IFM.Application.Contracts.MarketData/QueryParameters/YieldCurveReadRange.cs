namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

/// <summary>Bounds one inclusive Treasury yield-curve read.</summary>
public sealed record YieldCurveReadRange
{
    public const int MaximumDays = 3_660;

    public YieldCurveReadRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate.DayNumber - startDate.DayNumber + 1 > MaximumDays)
            throw new ArgumentOutOfRangeException(nameof(endDate), $"Yield-curve ranges may span at most {MaximumDays} days.");
        StartDate = startDate;
        EndDate = endDate;
    }

    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }
}
