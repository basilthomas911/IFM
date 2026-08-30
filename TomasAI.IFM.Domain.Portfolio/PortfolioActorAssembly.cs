using System.Reflection;

namespace TomasAI.IFM.Domain.Portfolio;

/// <summary>Provides the actor host with the Portfolio bounded-context assembly.</summary>
public static class PortfolioActorAssembly
{
    public static Assembly Current => typeof(PortfolioActorAssembly).Assembly;
}
