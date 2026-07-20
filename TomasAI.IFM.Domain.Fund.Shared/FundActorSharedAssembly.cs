using System.Reflection;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Provides access to the assembly that contains the shared types for the Fund Actor domain, such as common events, state models, and utility classes used across different components of the Fund Actor system.
/// </summary>
public static class FundActorSharedAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
