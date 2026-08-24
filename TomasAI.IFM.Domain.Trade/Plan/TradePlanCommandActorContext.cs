using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Defines the readonly runtime services required by <see cref="TradePlanCommandActor"/>.</summary>
public interface ITradePlanCommandActorContext : ICommandActorContext<TradePlanCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the EventProducer service supplied to the actor context.</summary>
    ITradeEventProducer EventProducer { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<TradePlanCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="TradePlanCommandActor"/>.</summary>
public sealed class TradePlanCommandActorContext : CommandActorContext, ICommandActorContext<TradePlanCommandActor>, ITradePlanCommandActorContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public TradePlanCommandActorContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ITradeEventProducer eventProducer,
        ILogger<TradePlanCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, TradePlanCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        EventProducer = IsArgumentNull.Set(eventProducer);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ITradeEventProducer EventProducer { get; }
    /// <inheritdoc/>
    public ILogger<TradePlanCommandActor> Logger { get; }
}
