using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Validation;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Extensions;

/// <summary>Exposes readonly FuturesTradeSignalCommand Command context properties.</summary>
public static class FuturesTradeSignalCommandContextExtensions
{
    extension(ICommandActorContext<FuturesTradeSignalCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesTradeSignalCommandContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesTradeSignalCommandContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbEventSource service retained by the typed context.</summary>
        public IEventSourceActorDbContext DbEventSource => context.DomainContext.DbEventSource;
        /// <summary>Gets the EventProjector service retained by the typed context.</summary>
        public IEventProjector<FuturesTradeSignalCommandActor> EventProjector => context.DomainContext.EventProjector;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesTradeSignalCommandActor> Logger => context.DomainContext.Logger;
    }
}
