using System.Collections.Concurrent;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.Blackboard;

/// <summary>
/// DI-facing Databento query contract whose mapping operations use the application Blackboard cache.
/// </summary>
public interface ICachedDatabentoMarketDataQueries : IDatabentoMarketDataQueries
{
}
