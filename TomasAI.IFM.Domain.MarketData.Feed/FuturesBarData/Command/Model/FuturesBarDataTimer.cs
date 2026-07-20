namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

/// <summary>
/// Provides functionality to manage a timer for executing periodic actions related to futures bar data.
/// </summary>
/// <remarks>This class allows starting and stopping a timer that executes a specified action at regular
/// intervals. The timer is configured to trigger the action every 60 seconds.</remarks>
public class FuturesBarDataTimer 
    : IFuturesBarDataTimer
{
    Timer? _futuresBarDataTimer;
    Action? _timerAction;

    /// <summary>
    /// Starts a timer that executes the specified action at regular intervals.
    /// </summary>
    /// <remarks>The timer begins execution immediately after this method is called. The provided action  will
    /// be invoked repeatedly at 60-second intervals. Ensure the action is thread-safe, as  it may be executed on a
    /// separate thread.</remarks>
    /// <param name="timerAction">The action to be executed by the timer. This action is invoked once every 60 seconds.</param>
    public void Start(Action timerAction)
    {
        _timerAction = timerAction;
        _futuresBarDataTimer = new Timer(_ => _timerAction?.Invoke(), null, 60000, 60000);
    }

    /// <summary>
    /// Stops the timer and releases associated resources.
    /// </summary>
    /// <remarks>This method cancels any ongoing timer actions and disposes of the timer instance, ensuring
    /// that  no further operations are performed. After calling this method, the timer cannot be restarted.</remarks>
    public void Stop()
    {
        _timerAction = null;
        _futuresBarDataTimer?.Dispose();
        _futuresBarDataTimer = null;
    }
}

public interface IFuturesBarDataTimer
{
    void Start(Action timerAction);
    void Stop();
}
