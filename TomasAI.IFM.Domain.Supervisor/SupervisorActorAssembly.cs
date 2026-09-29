using System.Reflection;

namespace TomasAI.IFM.Domain.Supervisor;

/// <summary>Exposes the Supervisor domain assembly for composition.</summary>
public static class SupervisorActorAssembly
{
    /// <summary>Gets the Supervisor domain assembly.</summary>
    public static Assembly Current { get; } = typeof(SupervisorActorAssembly).Assembly;
}
