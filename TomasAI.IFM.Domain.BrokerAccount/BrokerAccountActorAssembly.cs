using System.Reflection;

namespace TomasAI.IFM.Domain.BrokerAccount;

/// <summary>Exposes the BrokerAccount actor assembly to composition roots.</summary>
public static class BrokerAccountActorAssembly
{
    /// <summary>Gets the assembly containing BrokerAccount actors.</summary>
    public static Assembly Current => typeof(BrokerAccountActorAssembly).Assembly;
}
