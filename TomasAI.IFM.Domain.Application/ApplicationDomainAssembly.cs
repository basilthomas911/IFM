using System.Reflection;

namespace TomasAI.IFM.Domain.Application;

public static class ApplicationDomainAssembly
{
    public static Assembly Current => Assembly.GetExecutingAssembly();
}
