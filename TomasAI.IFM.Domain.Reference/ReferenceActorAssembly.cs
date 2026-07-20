using System.Reflection;

namespace TomasAI.IFM.Domain.Reference;

public static class ReferenceActorAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
