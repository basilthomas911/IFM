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

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesTradeSignalCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesTradeSignalCommandContext : CommandActorContext, IFuturesTradeSignalCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesTradeSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesTradeSignalCommandActor> eventProjector,
        ILogger<FuturesTradeSignalCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesTradeSignalCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc/>
    public IEventProjector<FuturesTradeSignalCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesTradeSignalCommandActor> Logger { get; }
}
