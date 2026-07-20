using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public class IBClientException : ApplicationException
    {
        private readonly int _clientId;
        private readonly int _errorCode;

        public int ClientId => _clientId;
        public int ErrorCode => _errorCode;

        public IBClientException(int clientId, int errorCode, string errorMsg, Exception ex)
            :base(errorMsg, ex)
        {
            _clientId = clientId;
            _errorCode = errorCode;
        }
    }
}
