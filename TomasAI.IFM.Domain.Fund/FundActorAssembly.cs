using System.Reflection;

namespace TomasAI.IFM.Domain.Fund;

/// <summary>
/// Provides access to the assembly that contains the FundActor types.
/// </summary>
public static class FundActorAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
