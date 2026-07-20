using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Service.TradePlacement.Model;

public class TradePlacementTimer 
    : ITradePlacementTimer
{
    const double DueTimeStart = 35;
    const double PeriodStart = 35;
    const double DecreaseOffset = -10;

    Action<TradePlacementId>? _timerAction;
    Timer? _tradePlacementTimer;
    TimeSpan _dueTime;
    TimeSpan _period;

    /// <summary>
    /// Initializes and starts a timer to handle trade placement events.
    /// </summary>
    /// <remarks>The timer is configured with an initial delay and a periodic interval, both defined in
    /// seconds.  If a timer is already active, it will be disposed and replaced with a new one.</remarks>
    /// <param name="e">The event containing details about the trade placement to be processed.</param>
    /// <param name="timerAction">An action to be invoked with the trade placement ID when the timer elapses.</param>
    /// <returns></returns>
    public  ValueTask StartAsync(TradePlacementStartedEvent e, Action<TradePlacementId> timerAction)
    {
        _timerAction = timerAction;
        _dueTime = TimeSpan.FromSeconds(DueTimeStart);
        _period = TimeSpan.FromSeconds(PeriodStart);
        _tradePlacementTimer?.Dispose();
        _tradePlacementTimer = null;
        _tradePlacementTimer = new Timer(_ => _timerAction?.Invoke(e.TradePlacementId), null, _dueTime, _period);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Stops the trade placement process and releases associated resources.
    /// </summary>
    /// <param name="e">The event containing details about the trade placement stop operation.</param>
    /// <returns>A completed <see cref="ValueTask"/> representing the asynchronous stop operation.</returns>
    public ValueTask StopAsync(TradePlacementStoppedEvent e)
    {
        _tradePlacementTimer?.Dispose();
        _tradePlacementTimer = null;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Waits asynchronously and schedules a timer to invoke the specified action for the given trade placement.
    /// </summary>
    /// <remarks>This method adjusts the timer's due time and period by adding a predefined offset, disposes
    /// of any existing timer, and creates a new timer to execute the associated action. The timer action is invoked
    /// with the provided trade placement identifier.</remarks>
    /// <param name="e">The trade placement identifier associated with the timer action.</param>
    /// <returns>A completed <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public ValueTask WaitAsync(TradePlacementId e)
    {
        _dueTime = _dueTime.Add(TimeSpan.FromSeconds(DecreaseOffset));
        _period = _period.Add(TimeSpan.FromSeconds(DecreaseOffset));
        _tradePlacementTimer?.Dispose();
        _tradePlacementTimer = null;
        _tradePlacementTimer = new Timer(_ => _timerAction?.Invoke(e), null, _dueTime, _period);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resets the internal timer with the specified trade placement identifier.
    /// </summary>
    /// <remarks>This method initializes a new timer with predefined start and period durations.  Any
    /// previously existing timer is disposed before the new timer is created.</remarks>
    /// <param name="e">The trade placement identifier used when invoking the timer action.</param>
    /// <returns>A completed <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public ValueTask ResetAsync(TradePlacementId e)
    {
        _dueTime = TimeSpan.FromSeconds(DueTimeStart);
        _period = TimeSpan.FromSeconds(PeriodStart);
        _tradePlacementTimer?.Dispose();
        _tradePlacementTimer = null;
        _tradePlacementTimer = new Timer(_ => _timerAction?.Invoke(e), null, _dueTime, _period);
        return ValueTask.CompletedTask;
    }

}

public interface ITradePlacementTimer
{
    ValueTask StartAsync(TradePlacementStartedEvent e, Action<TradePlacementId> timerAction);
    ValueTask StopAsync(TradePlacementStoppedEvent e);
    ValueTask ResetAsync(TradePlacementId e);
    ValueTask WaitAsync(TradePlacementId e);
}
