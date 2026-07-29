using System.Reflection;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>
/// Provides access to the assembly containing the shared Trade domain contracts.
/// </summary>
public static class TradeSharedAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
