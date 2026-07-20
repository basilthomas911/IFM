using System.Reflection;

namespace TomasAI.IFM.Domain.MarketData.Feed;

/// <summary>
/// Provides access to the assembly containing the market data feed actor types.
/// </summary>
/// <remarks>Use this class to retrieve the assembly for reflection or registration purposes related to market
/// data feed actors. This is useful when dynamically loading types or resources associated with the feed actor
/// implementation.</remarks>
public static class MarketDataFeedActorAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
