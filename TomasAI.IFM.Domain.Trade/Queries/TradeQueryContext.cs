using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Queries.Handlers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Queries;

/// <summary>Defines the readonly runtime services required by <see cref="TradeQueryActor"/>.</summary>
public interface ITradeQueryContext : IQueryActorContext<TradeQueryActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the blackboard service supplied to the actor context.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<TradeQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="TradeQueryActor"/>.</summary>
public sealed class TradeQueryContext : QueryActorContext, IQueryActorContext<TradeQueryActor>, ITradeQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public TradeQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<TradeQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, TradeQueryActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        BlackboardService = IsArgumentNull.Set(blackboardService);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public IBlackboardService BlackboardService { get; }
    /// <inheritdoc/>
    public ILogger<TradeQueryActor> Logger { get; }
}
