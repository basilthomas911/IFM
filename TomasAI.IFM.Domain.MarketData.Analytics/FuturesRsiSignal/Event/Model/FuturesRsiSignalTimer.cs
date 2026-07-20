using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;

public static class FuturesRsiSignalTimer 
{
    static Dictionary<FuturesRsiSignalEntityId, Action<FuturesRsiSignalEntityId>?> _timerActionMap =[];
    static Dictionary<FuturesRsiSignalEntityId, Timer?> _rsiSignalTimerMap = [];

    /// <summary>
    /// Starts the timer for the specified RSI signal event and associates it with the provided action to be executed on each timer tick.
    /// </summary>
    /// <param name="e"> </param>
    /// <param name="timerAction"> </param>
    public static void StartTimer(this FuturesRsiSignalStartedEvent e, Action<FuturesRsiSignalEntityId> timerAction)
    {
        if (!_rsiSignalTimerMap.TryGetValue(e.EntityId, out var existingTimer))
        {
            _timerActionMap[e.EntityId] = timerAction;
            _rsiSignalTimerMap[e.EntityId] = new Timer(_ => timerAction(e.EntityId), null, TimeSpan.Zero, GetTimerPeriod(e.EntityId));
        }
        
        static TimeSpan GetTimerPeriod(FuturesRsiSignalEntityId rsiSignalId)
            => rsiSignalId.TimePeriod switch
            {
                TradeTimePeriodType.Daily => TimeSpan.FromMinutes(1),
                TradeTimePeriodType.Weekly => TimeSpan.FromMinutes(15),
                TradeTimePeriodType.WeekMonthBridge => TimeSpan.FromHours(1),
                TradeTimePeriodType.Monthly => TimeSpan.FromDays(1),
                _ => TimeSpan.FromMinutes(1)
            };
    }

    /// <summary>
    /// Stops the timer associated with the specified RSI signal event and removes it from the internal tracking dictionaries.
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static void StopTimer(this FuturesRsiSignalStoppedEvent e)
    {
        if (_rsiSignalTimerMap.Remove(e.EntityId, out var timer))
        {
            timer.Dispose();
            _rsiSignalTimerMap.Remove(e.EntityId);
        }
        _timerActionMap.Remove(e.EntityId); 
    }

}

