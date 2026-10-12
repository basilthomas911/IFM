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

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Extensions;

/// <summary>Exposes readonly FuturesTradeSignalCommand Command context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesTradeSignalCommandContextExtensions
{
    extension(ICommandActorContext<FuturesTradeSignalCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesTradeSignalCommandContext DomainContext =>
            IsArgumentNull.Named(context as IFuturesTradeSignalCommandContext, nameof(context))!;
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
