using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketData
{
    public interface ITickFeedResult
    {
        bool Succeeded { get; }
        string ErrorMessage { get; }
    }
}
