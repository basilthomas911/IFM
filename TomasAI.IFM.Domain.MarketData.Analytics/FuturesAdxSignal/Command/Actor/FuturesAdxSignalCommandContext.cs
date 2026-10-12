using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesAdxSignalCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesAdxSignalCommandContext : CommandActorContext, IFuturesAdxSignalCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesAdxSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesAdxSignalCommandActor> eventProjector,
        ILogger<FuturesAdxSignalCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesAdxSignalCommandActor.ActorName))
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
    public IEventProjector<FuturesAdxSignalCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesAdxSignalCommandActor> Logger { get; }
}
