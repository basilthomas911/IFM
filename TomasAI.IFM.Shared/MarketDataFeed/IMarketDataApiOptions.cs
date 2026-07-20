using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public interface IMarketDataApiOptions
{
    string Host { get; }
    int Port { get; }
    int ClientId { get; }
}

public interface IBrokerDataApiOptions : IMarketDataApiOptions
{
}

public interface IMarketDataSnapshotApiOptions : IMarketDataApiOptions
{

}
