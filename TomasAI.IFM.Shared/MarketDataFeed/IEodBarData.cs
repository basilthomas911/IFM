using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public interface IEodBarData
    {
        DateOnly valueDate { get; }
        string ContractId { get; }
        double OpenPrice { get; }
        double HighPrice { get; }
        double LowPrice { get; }
        double ClosePrice { get; }
        int Volume { get; }
   
    }
}
