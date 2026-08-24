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
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Option.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="OptionTradeCommandActor"/>.</summary>
public interface IOptionTradeCommandContext : ICommandActorContext<OptionTradeCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<OptionTradeCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<OptionTradeCommandActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="OptionTradeCommandActor"/>.</summary>
public sealed class OptionTradeCommandContext : CommandActorContext, ICommandActorContext<OptionTradeCommandActor>, IOptionTradeCommandContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public OptionTradeCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IDbContextFactory dbFactory,
        IEventProjector<OptionTradeCommandActor> eventProjector,
        ILogger<OptionTradeCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, OptionTradeCommandActor.ActorName))
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
    public IEventProjector<OptionTradeCommandActor> EventProjector { get; }
    /// <inheritdoc/>
    public ILogger<OptionTradeCommandActor> Logger { get; }
}
