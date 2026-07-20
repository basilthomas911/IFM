using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public class ContractDetailsMessage
    {
        private readonly int _requestId;
        private readonly ContractDetails _contractDetails;

        public int RequestId => _requestId;
        public ContractDetails ContractDetails => _contractDetails;

        public ContractDetailsMessage(int requestId, ContractDetails contractDetails)
        {
            _requestId = requestId;
            _contractDetails = contractDetails;
        }
    }
}
