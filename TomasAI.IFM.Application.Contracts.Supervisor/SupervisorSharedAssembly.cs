using System.Reflection;

namespace TomasAI.IFM.Domain.Supervisor.Shared;

/// <summary>Exposes the Supervisor shared-contract assembly.</summary>
public static class SupervisorSharedAssembly
{
    /// <summary>Gets the assembly name reserved for Supervisor actor implementations.</summary>
    public const string ActorAssemblyName = "TomasAI.IFM.Domain.Supervisor";

    /// <summary>Gets the Supervisor shared-contract assembly.</summary>
    public static Assembly Current { get; } = typeof(SupervisorSharedAssembly).Assembly;
}
