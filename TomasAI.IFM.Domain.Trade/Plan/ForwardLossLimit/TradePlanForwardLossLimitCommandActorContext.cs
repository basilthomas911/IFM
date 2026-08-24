using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>Defines the readonly runtime services required by <see cref="TradePlanForwardLossLimitCommandActor"/>.</summary>
public interface ITradePlanForwardLossLimitCommandActorContext : ICommandActorContext<TradePlanForwardLossLimitCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the EventProducer service supplied to the actor context.</summary>
    ITradeEventProducer EventProducer { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<TradePlanForwardLossLimitCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="TradePlanForwardLossLimitCommandActor"/>.</summary>
public sealed class TradePlanForwardLossLimitCommandActorContext : CommandActorContext, ICommandActorContext<TradePlanForwardLossLimitCommandActor>, ITradePlanForwardLossLimitCommandActorContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public TradePlanForwardLossLimitCommandActorContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ITradeEventProducer eventProducer,
        ILogger<TradePlanForwardLossLimitCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, TradePlanForwardLossLimitCommandActor.ActorName))
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
    public ILogger<TradePlanForwardLossLimitCommandActor> Logger { get; }
}
