using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Actor.Client.Extensions;

/// <summary>Exposes readonly services retained by the test event context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "Extension receiver syntax is not an externally nested type.")]
public static class TestEventContextExtensions
{
    extension(IEventActorContext<TestEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension receiver properties depend on the instance context and cannot be static receiver properties.")]
        public ITestEventContext DomainContext =>
            IsArgumentNull.Set(context as ITestEventContext)!;

        /// <summary>Gets the actor supervisor.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension receiver properties depend on the instance context and cannot be static receiver properties.")]
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the actor logger.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension receiver properties depend on the instance context and cannot be static receiver properties.")]
        public ILogger<TestEventActor> Logger => context.DomainContext.Logger;
    }
}
