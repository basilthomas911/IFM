using System.Reflection;

namespace TomasAI.IFM.Application.Actor;

public static class ApplicationActorAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
