using System.Reflection;

namespace TomasAI.IFM.Domain.Trade;

public static class TradeActorAssembly
{
    public static Assembly Current { get; } = typeof(TradeActorAssembly).Assembly;
}
