using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TomasAI.IFM.Service.MarketDataFeed
{
    public class FuturesBarDataTimer : IFuturesBarDataTimer
    {
        Timer _futuresBarDataTimer;
        Action _timerAction;

        public void Start(Action timerAction)
        {
            _timerAction = timerAction;
            _futuresBarDataTimer = new Timer(_ => _timerAction?.Invoke(), null, 60000, 60000);
        }

        public void Stop()
        {
            _timerAction = null;
            _futuresBarDataTimer?.Dispose();
            _futuresBarDataTimer = null;
        }
    }
}
