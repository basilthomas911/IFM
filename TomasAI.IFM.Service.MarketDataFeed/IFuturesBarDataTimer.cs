using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Service.MarketDataFeed
{
    public  interface IFuturesBarDataTimer
    {
        void Start(Action timerAction);
        void Stop();    
    }
}
