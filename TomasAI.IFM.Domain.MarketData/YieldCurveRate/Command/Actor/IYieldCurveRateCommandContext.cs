using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Actor;

/// <summary>Defines the runtime services required by <see cref="YieldCurveRateCommandActor"/>.</summary>
public interface IYieldCurveRateCommandContext : ICommandActorContext<YieldCurveRateCommandActor>
{
    /// <summary>Gets the database factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<YieldCurveRateCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets the actor service.</summary>
    IActorService ActorService { get; }
}
