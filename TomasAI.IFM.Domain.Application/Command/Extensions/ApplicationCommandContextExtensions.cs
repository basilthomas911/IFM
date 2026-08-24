using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Application.Actor.Command.Handlers;
using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Application.Actor.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Application.Actor.Command.Extensions;

/// <summary>Exposes readonly ApplicationCommand Command context properties.</summary>
public static class ApplicationCommandContextExtensions
{
    extension(ICommandActorContext<ApplicationCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IApplicationCommandContext DomainContext =>
            IsArgumentNull.Set(context as IApplicationCommandContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbEventSource service retained by the typed context.</summary>
        public IEventSourceActorDbContext DbEventSource => context.DomainContext.DbEventSource;
        /// <summary>Gets the EventProjector service retained by the typed context.</summary>
        public IEventProjector<ApplicationCommandActor> EventProjector => context.DomainContext.EventProjector;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<ApplicationCommandActor> Logger => context.DomainContext.Logger;
    }
}
