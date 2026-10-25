using System.Reflection;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Provides access to the assembly containing the shared market data analytics contracts.
/// </summary>
public static class MarketDataAnalyticsSharedAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
