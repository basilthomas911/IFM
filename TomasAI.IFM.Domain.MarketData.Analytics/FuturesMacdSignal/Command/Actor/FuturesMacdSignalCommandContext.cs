using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Actor;

/// <summary>Provides the typed runtime context used by <see cref="FuturesMacdSignalCommandActor"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesMacdSignalCommandContext : CommandActorContext, IFuturesMacdSignalCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesMacdSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IDbContextFactory dbFactory,
        IEventProjector<FuturesMacdSignalCommandActor> eventProjector,
        ILogger<FuturesMacdSignalCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, FuturesMacdSignalCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        DbFactory = IsArgumentNull.Set(dbFactory);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IEventProjector<FuturesMacdSignalCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<FuturesMacdSignalCommandActor> Logger { get; }
}
