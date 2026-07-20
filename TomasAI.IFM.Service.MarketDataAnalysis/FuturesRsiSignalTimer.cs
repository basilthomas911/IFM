using QLNet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Service.MarketDataAnalytics
{
    public class FuturesRsiSignalTimer : IFuturesRsiSignalTimer
    {
        Action<FuturesRsiSignalEntityId>? _timerAction;
        Timer? _rsiSignalTimer;

        /// <summary>
        /// start futures rsi signal timer 
        /// </summary>
        /// <param name="timerAction"></param>
        public async Task StartAsync(FuturesRsiSignalStartedEvent e, Action<FuturesRsiSignalEntityId> timerAction)
        {
            _timerAction = timerAction;
            _rsiSignalTimer?.Dispose();
            _rsiSignalTimer = null;
            _rsiSignalTimer = new Timer(_ => _timerAction?.Invoke(e.EntityId), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
            await Task.CompletedTask;
        }

        /// <summary>
        /// stop futures rsi signal timer
        /// </summary>
        public async Task StopAsync(FuturesRsiSignalStoppedEvent e)
        {
            _rsiSignalTimer?.Dispose();
            _rsiSignalTimer = null;
            await Task.CompletedTask;
        }

    }
}
