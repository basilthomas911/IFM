using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;

namespace TomasAI.IFM.Framework.MarketData.InteractiveBrokers.Messages
{
    public record ContractDetailsMessage(
        int RequestId,
        ContractDetails ContractDetails)
    {
    }
}
