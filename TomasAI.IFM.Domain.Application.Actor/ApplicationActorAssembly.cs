using System.Reflection;

namespace TomasAI.IFM.Domain.Application.Actor;

public static class ApplicationActorAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
