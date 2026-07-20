using System.Reflection;

namespace TomasAI.IFM.Domain.MarketData.Analytics;

/// <summary>
/// Provides access to the assembly containing the market data analytics actor types.
/// </summary>
/// <remarks>Use this class to retrieve the assembly for reflection or registration purposes related to market
/// data analytics actors. This is useful when dynamically loading types or resources associated with the analytics actor
/// implementation.</remarks>
public static class MarketDataAnalyticsActorAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
