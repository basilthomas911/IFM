using System.Reflection;

namespace TomasAI.IFM.Domain.SystemAdmin;

public static class SystemAdminActorAssembly
{
    public static Assembly Current { get; } = typeof(SystemAdminActorAssembly).Assembly;
}
