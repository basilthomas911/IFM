namespace TomasAI.IFM.Domain.MarketData.Shared;
/// <summary>Applies persisted, successful market-close completions to the operational date projection.</summary>
public interface ICompletedFuturesEndOfDayProjection
{
    /// <summary>Advances after successful market close. Older or duplicate completions have no effect.</summary>
    /// <param name="completedValueDate">The exchange date whose market-close workflow completed.</param>
    /// <returns>True when a newer persisted completion was applied.</returns>
    bool ApplyCompletedEndOfDay(DateOnly completedValueDate);
}
