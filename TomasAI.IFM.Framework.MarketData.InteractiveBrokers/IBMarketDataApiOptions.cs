using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Framework.MarketData.InteractiveBrokers;

public class IBMarketDataApiOptions(string host, int port, int clientId) 
    : IMarketDataApiOptions
{
    public string Host { get; } = host;

    public int Port { get; } = port;

    public int ClientId { get; } = clientId;

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
