using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Extensions;

/// <summary>Exposes yield-curve command services as readonly extension properties.</summary>
public static class YieldCurveRateCommandExtensions
{
    extension(ICommandActorContext<YieldCurveRateCommandActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IYieldCurveRateCommandContext YieldCurveRateContext => IsArgumentNull.Set(context as IYieldCurveRateCommandContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.YieldCurveRateContext.DbFactory;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<YieldCurveRateCommandActor> Logger => context.YieldCurveRateContext.Logger;
        /// <summary>Gets the event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.YieldCurveRateContext.DbEventSource;
        /// <summary>Gets the state factory.</summary>
        public IEventSourceActorStateFactory StateFactory => context.YieldCurveRateContext.StateFactory;
        /// <summary>Gets the actor service.</summary>
        public IActorService ActorService => context.YieldCurveRateContext.ActorService;
    }
}
