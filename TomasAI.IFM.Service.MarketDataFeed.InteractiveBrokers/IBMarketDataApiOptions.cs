using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers
{
    public class IBMarketDataApiOptions : IMarketDataApiOptions
    {
  
        public IBMarketDataApiOptions(string host, int port, int clientId)
        {
            Host = host;
            Port = port;
            ClientId = clientId;
        }

        public string Host { get; }

        public int Port { get; }

        public int ClientId { get; }

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
