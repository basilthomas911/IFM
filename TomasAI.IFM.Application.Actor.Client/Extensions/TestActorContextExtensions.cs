using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Actor.Client.Extensions;

/// <summary>Exposes readonly services retained by the test command context.</summary>
public static class TestCommandContextExtensions
{
    extension(ICommandActorContext<TestCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ITestCommandContext DomainContext =>
            IsArgumentNull.Set(context as ITestCommandContext, nameof(context))!;

        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the actor logger.</summary>
        public ILogger<TestCommandActor> Logger => context.DomainContext.Logger;
    }
}

/// <summary>Exposes readonly services retained by the test event context.</summary>
public static class TestEventContextExtensions
{
    extension(IEventActorContext<TestEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ITestEventContext DomainContext =>
            IsArgumentNull.Set(context as ITestEventContext, nameof(context))!;

        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the actor logger.</summary>
        public ILogger<TestEventActor> Logger => context.DomainContext.Logger;
    }
}

/// <summary>Exposes readonly services retained by the test query context.</summary>
public static class TestQueryContextExtensions
{
    extension(IQueryActorContext<TestQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ITestQueryContext DomainContext =>
            IsArgumentNull.Set(context as ITestQueryContext, nameof(context))!;

        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the actor logger.</summary>
        public ILogger<TestQueryActor> Logger => context.DomainContext.Logger;
    }
}
