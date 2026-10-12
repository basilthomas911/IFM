namespace TomasAI.IFM.Domain.MarketData.Shared;
/// <summary>Applies only persisted, successful whole-session EOD completion observations to the operational date projection.</summary>
public interface ICompletedFuturesEndOfDayProjection
{
    /// <summary>Advances after successful EOD; older or duplicate completion observations have no effect.</summary>
    /// <param name="completedValueDate">The exchange date whose position EOD commands all succeeded.</param>
    /// <returns>True when a newer persisted completion was applied.</returns>
    bool ApplyCompletedEndOfDay(DateOnly completedValueDate);
}
