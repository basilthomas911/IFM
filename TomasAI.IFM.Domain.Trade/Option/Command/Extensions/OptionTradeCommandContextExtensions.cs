using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Validation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Option.Command.Validation;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Option.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Option.Command.Extensions;

/// <summary>Exposes readonly OptionTradeCommand Command context properties.</summary>
public static class OptionTradeCommandContextExtensions
{
    extension(ICommandActorContext<OptionTradeCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IOptionTradeCommandContext DomainContext =>
            IsArgumentNull.Set(context as IOptionTradeCommandContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbEventSource service retained by the typed context.</summary>
        public IEventSourceActorDbContext DbEventSource => context.DomainContext.DbEventSource;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the EventProjector service retained by the typed context.</summary>
        public IEventProjector<OptionTradeCommandActor> EventProjector => context.DomainContext.EventProjector;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<OptionTradeCommandActor> Logger => context.DomainContext.Logger;
    }
}
