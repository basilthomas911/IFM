using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesMacdSignalQueryActor"/>.</summary>
public interface IFuturesMacdSignalQueryContext : IQueryActorContext<FuturesMacdSignalQueryActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesMacdSignalQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="FuturesMacdSignalQueryActor"/>.</summary>
public sealed class FuturesMacdSignalQueryContext : QueryActorContext, IQueryActorContext<FuturesMacdSignalQueryActor>, IFuturesMacdSignalQueryContext
{
    /// <summary>Initializes a new typed actor context.</summary>
    public FuturesMacdSignalQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        ILogger<FuturesMacdSignalQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, FuturesMacdSignalQueryActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }
    /// <inheritdoc/>
    public IDbContextFactory DbFactory { get; }
    /// <inheritdoc/>
    public ILogger<FuturesMacdSignalQueryActor> Logger { get; }
}
