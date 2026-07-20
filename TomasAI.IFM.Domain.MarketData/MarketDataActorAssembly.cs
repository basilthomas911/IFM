using System.Reflection;

namespace TomasAI.IFM.Domain.MarketData;

/// <summary>
/// Provides access to the assembly that contains the MarketDataActor types.
/// </summary>
public static class MarketDataActorAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
