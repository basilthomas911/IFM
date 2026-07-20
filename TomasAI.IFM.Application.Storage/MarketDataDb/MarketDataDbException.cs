using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Storage.MarketDataDb
{
    public class TradeDbException : Exception
    {
        public TradeDbException(string errorMsg, Exception innerException)
            :base(errorMsg, innerException)
        {
        }
    }
}
