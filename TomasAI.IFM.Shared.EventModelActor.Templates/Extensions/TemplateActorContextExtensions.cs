using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor.Templates.Extensions;

/// <summary>Exposes readonly services retained by the command template context.</summary>
public static class CommandActorTemplateContextExtensions
{
    extension(ICommandActorContext<CommandActorTemplate> context)
    {
        /// <summary>Gets the template-specific typed context.</summary>
        public ICommandActorTemplateContext DomainContext =>
            IsArgumentNull.Set(context as ICommandActorTemplateContext, nameof(context))!;

        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the event-source database context.</summary>
        public IEventSourceActorDbContext DbEventSource => context.DomainContext.DbEventSource;

        /// <summary>Gets the actor logger.</summary>
        public ILogger<CommandActorTemplate> Logger => context.DomainContext.Logger;
    }
}

/// <summary>Exposes readonly services retained by the event template context.</summary>
public static class EventActorTemplateContextExtensions
{
    extension(IEventActorContext<EventActorTemplate> context)
    {
        /// <summary>Gets the template-specific typed context.</summary>
        public IEventActorTemplateContext DomainContext =>
            IsArgumentNull.Set(context as IEventActorTemplateContext, nameof(context))!;

        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the actor logger.</summary>
        public ILogger<EventActorTemplate> Logger => context.DomainContext.Logger;
    }
}

/// <summary>Exposes readonly services retained by the query template context.</summary>
public static class QueryActorTemplateContextExtensions
{
    extension(IQueryActorContext<QueryActorTemplate> context)
    {
        /// <summary>Gets the template-specific typed context.</summary>
        public IQueryActorTemplateContext DomainContext =>
            IsArgumentNull.Set(context as IQueryActorTemplateContext, nameof(context))!;

        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;

        /// <summary>Gets the database-context factory.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;

        /// <summary>Gets the actor logger.</summary>
        public ILogger<QueryActorTemplate> Logger => context.DomainContext.Logger;
    }
}
