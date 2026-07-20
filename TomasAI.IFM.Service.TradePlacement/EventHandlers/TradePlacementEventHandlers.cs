using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.TradePlacement.EventHandlers;

public class TradePlacementEventHandlers(IStatusConsoleWriter statusConsoleWriter) 
    : BaseEventServiceHandler(statusConsoleWriter),
        IAsyncEventServiceHandler<TradePlacementStartedEvent, TradePlacementEventService>,
        IAsyncEventServiceHandler<TradePlacementStoppedEvent, TradePlacementEventService>,
        IAsyncEventServiceHandler<TradePlacementSetEvent, TradePlacementEventService>,
        IAsyncEventServiceHandler<TradePlacementWaitEvent, TradePlacementEventService>,
        IAsyncEventServiceHandler<TradePlacementClearedEvent, TradePlacementEventService>
{
   /// <summary>
   /// Initiates the trade placement process by starting a timer that triggers periodic trade signal evaluations.
   /// </summary>
   /// <remarks>This method starts a timer using the provided <paramref name="s"/> service. The timer
   /// periodically invokes  a callback to evaluate trade signals and trigger trade placement commands if valid signals
   /// are found.  If an exception occurs while starting the timer, the error is logged, and the method completes
   /// without throwing.</remarks>
   /// <param name="e">The event containing details about the trade placement, including the contract ID and value date.</param>
   /// <param name="s">The service responsible for managing the trade placement timer.</param>
   /// <returns></returns>
    public async Task ExecuteAsync(TradePlacementStartedEvent e, TradePlacementEventService s)
    {
        try
        {
            await s.TradePlacementTimer.StartAsync(e, async o => await TimerTickAsync(o));
            return;
        }
        catch(Exception ex)
        {
            s.Logger.LogError($"TradePlacementEventHandlers.ExecuteAsync: TradePlacementStartedEvent - Unable to start timer due to {ex.Message}");
        }

        async Task TimerTickAsync(TradePlacementId e)
        {
            try
            {
                // get last updated futures trade signal...
                var sr = await s.MarketDataAnalyticsQueryApi.GetFuturesTradeSignalAsync(e.ContractId, e.ValueDate);
                if (sr is not null && sr.Success && sr.Value is not null)
                    await s.TradePlacementCommandApi.SignalTradePlacementAsync(sr.Value);
            }
            catch (Exception ex)
            {
                s.Logger.LogError($"TradePlacementEventHandlers.ExecuteAsync: TimerTickAsync failed due to {ex.Message}");
            }
        }
    }

   /// <summary>
   /// Handles the <see cref="TradePlacementStoppedEvent"/> by stopping the associated trade placement timer
   /// asynchronously.
   /// </summary>
   /// <remarks>This method attempts to stop the trade placement timer using the provided service.  If an
   /// exception occurs during the operation, the error is logged using the service's logger.</remarks>
   /// <param name="e">The event containing details about the stopped trade placement.</param>
   /// <param name="s">The service responsible for managing trade placement operations.</param>
   /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(TradePlacementStoppedEvent e, TradePlacementEventService s)
    {
        try
        {
            await s.TradePlacementTimer.StopAsync(e);
        }
        catch (Exception ex)
        {
            s.Logger.LogError($"TradePlacementEventHandlers.ExecuteAsync: TradePlacementStoppedEvent failed due to {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the specified trade placement set event asynchronously.
    /// </summary>
    /// <remarks>This method resets the trade placement timer for the specified trade placement and logs the
    /// event details. If an exception occurs during execution, the error is logged.</remarks>
    /// <param name="e">The event containing details about the trade placement to be processed. Cannot be null.</param>
    /// <param name="s">The service used to handle trade placement operations. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(TradePlacementSetEvent e, TradePlacementEventService s)
    {
        try
        {
            await s.TradePlacementTimer.ResetAsync(e.TradePlacementId);
            await WriteConsoleAsync(LogSourceType.TradePlacement, $"Trade Placement: {e.TradePlacementId.ContractId} - SET @ {e.FuturesTradeSignal.FuturesPrice:F2}");
        }
        catch (Exception ex)
        {
            s.Logger.LogError($"TradePlacementEventHandlers.ExecuteAsync: TradePlacementSetEvent failed due to {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the trade placement wait operation asynchronously.
    /// </summary>
    /// <remarks>This method waits for the specified trade placement to complete using the provided service's
    /// timer mechanism.  If the operation succeeds, a log entry is written to the console with details about the trade
    /// placement.  If an exception occurs during the operation, it is logged as an error.</remarks>
    /// <param name="e">The event containing details about the trade placement, including the trade placement ID and associated trade
    /// signal.</param>
    /// <param name="s">The service responsible for handling trade placement operations, including logging and timer management.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(TradePlacementWaitEvent e, TradePlacementEventService s)
    {
        try
        {
            await s.TradePlacementTimer.WaitAsync(e.TradePlacementId);
            await WriteConsoleAsync(LogSourceType.TradePlacement, $"Trade Placement: {e.TradePlacementId.ContractId} - WAIT @ {e.FuturesTradeSignal.FuturesPrice:F2}");
        }
        catch (Exception ex)
        {
            s.Logger.LogError($"TradePlacementEventHandlers.ExecuteAsync: TradePlacementWaitEvent failed due to {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the processing of a cleared trade placement event asynchronously.
    /// </summary>
    /// <remarks>This method resets the trade placement timer for the specified trade placement and logs the
    /// cleared trade placement details. If an exception occurs during processing, the error is logged using the
    /// provided service's logger.</remarks>
    /// <param name="e">The event containing details about the cleared trade placement, including the trade placement ID and associated
    /// data.</param>
    /// <param name="s">The service responsible for managing trade placements, including logging and timer operations.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(TradePlacementClearedEvent e, TradePlacementEventService s)
    {
        try
        {
            await s.TradePlacementTimer.ResetAsync(e.TradePlacementId);
            await WriteConsoleAsync(LogSourceType.TradePlacement, $"Trade Placement: {e.TradePlacementId.ContractId} - CLEARED @ {e.FuturesTradeSignal.FuturesPrice:F2}");
        }
        catch (Exception ex)
        {
            s.Logger.LogError($"TradePlacementEventHandlers.ExecuteAsync: TradePlacementClearedEvent failed due to {ex.Message}");
        }
    }
}
