using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Service.MarketDataAnalytics
{
    public class FuturesTradeSignalLLMTimer : IFuturesTradeSignalLLMTimer
    {
        Action<FuturesTradeSignalId>? _timerAction;
        Timer? _tradePlacementTimer;

        /// <summary>
        /// start futures trade signal LLM timer 
        /// </summary>
        /// <param name="timerAction"></param>
        public async Task StartAsync(FuturesTradeSignalLLMStartedEvent e, Action<FuturesTradeSignalId> timerAction)
        {
            _timerAction = timerAction;
            _tradePlacementTimer?.Dispose();
            _tradePlacementTimer = null;
            _tradePlacementTimer = new Timer(_ => _timerAction?.Invoke(e.EntityId), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
            await Task.CompletedTask;
        }

        /// <summary>
        /// stop futures trade signal LLM timer
        /// </summary>
        public async Task StopAsync(FuturesTradeSignalLLMStoppedEvent e)
        {
            _tradePlacementTimer?.Dispose();
            _tradePlacementTimer = null;
            await Task.CompletedTask;
        }

    }
}
