using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Option.Event.Extensions;
using TomasAI.IFM.Domain.Trade.Option.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Option.Event.Extensions;

/// <summary>Exposes readonly OptionTradeEvent Event context properties.</summary>
public static class OptionTradeEventContextExtensions
{
    extension(IEventActorContext<OptionTradeEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IOptionTradeEventContext DomainContext =>
            IsArgumentNull.Set(context as IOptionTradeEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the StatusConsoleWriter service retained by the typed context.</summary>
        public IStatusConsoleWriter StatusConsoleWriter => context.DomainContext.StatusConsoleWriter;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<OptionTradeEventActor> Logger => context.DomainContext.Logger;
    }
}
