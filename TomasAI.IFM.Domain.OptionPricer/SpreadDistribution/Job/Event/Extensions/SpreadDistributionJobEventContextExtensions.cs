using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Extensions;

/// <summary>Exposes readonly SpreadDistributionJobEvent Event context properties.</summary>
public static class SpreadDistributionJobEventContextExtensions
{
    extension(IEventActorContext<SpreadDistributionJobEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ISpreadDistributionJobEventContext DomainContext =>
            IsArgumentNull.Set(context as ISpreadDistributionJobEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the StatusConsoleWriter service retained by the typed context.</summary>
        public IStatusConsoleWriter StatusConsoleWriter => context.DomainContext.StatusConsoleWriter;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<SpreadDistributionJobEventActor> Logger => context.DomainContext.Logger;
    }
}
