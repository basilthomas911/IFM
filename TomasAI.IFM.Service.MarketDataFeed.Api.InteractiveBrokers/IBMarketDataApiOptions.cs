using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.Api.InteractiveBrokers
{
    public class IBMarketDataApiOptions : IMarketDataApiOptions
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _clientId;

        public string Host => _host;

        public int Port => _port;

        public int ClientId => _clientId;

        public IBMarketDataApiOptions(string host, int port, int clientId)
        {
            _host = host;
            _port = port;
            _clientId = clientId;
        }
    }

    public class IBBrokerDataApiOptions : IBMarketDataApiOptions, IBrokerDataApiOptions
    {
        public IBBrokerDataApiOptions(string host, int port, int clientId)
            :base(host, port, clientId)
        {
        }
    }

    public class IBMarketDataSnapshotApiOptions : IBMarketDataApiOptions, IMarketDataSnapshotApiOptions
    {
        public IBMarketDataSnapshotApiOptions(string host, int port, int clientId)
            : base(host, port, clientId)
        {
        }
    }
}
