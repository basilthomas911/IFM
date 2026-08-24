using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Option.Query.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="OptionTradeQueryActor"/>.</summary>
public interface IOptionTradeQueryContext : IQueryActorContext<OptionTradeQueryActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the BlackboardService service supplied to the actor context.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<OptionTradeQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="OptionTradeQueryActor"/>.</summary>
public sealed class OptionTradeQueryContext : QueryActorContext, IQueryActorContext<OptionTradeQueryActor>, IOptionTradeQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public OptionTradeQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IBlackboardService blackboardService,
        ILogger<OptionTradeQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, OptionTradeQueryActor.ActorName))
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
    public ILogger<OptionTradeQueryActor> Logger { get; }
}
