using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Extensions;

/// <summary>Exposes readonly Market Outlook command services from the closed-generic context.</summary>
public static class MarketOutlookSnapshotCommandContextExtensions
{
    extension(ICommandActorContext<MarketOutlookSnapshotCommandActor> context)
    {
        /// <summary>Gets the domain-specific command context.</summary>
        public IMarketOutlookSnapshotCommandContext DomainContext =>
            IsArgumentNull.Set(
                context as IMarketOutlookSnapshotCommandContext,
                nameof(context))!;

        /// <summary>Gets the transactional event-source database.</summary>
        public IEventSourceActorDbContext DbEventSource => context.DomainContext.DbEventSource;

        /// <summary>Gets the application database-context factory.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;

        /// <summary>Gets the authoritative event-source repository.</summary>
        public IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState> StateRepository =>
            context.DomainContext.StateRepository;

        /// <summary>Gets the command-owned event projector.</summary>
        public IEventProjector<MarketOutlookSnapshotCommandActor> EventProjector =>
            context.DomainContext.EventProjector;

        /// <summary>Gets the durable projection queue.</summary>
        public IDurableReplayQueue DurableReplayQueue => context.DomainContext.DurableReplayQueue;

        /// <summary>Gets the projector blackboard.</summary>
        public IBlackboardService BlackboardService => context.DomainContext.BlackboardService;

        /// <summary>Gets the system clock.</summary>
        public TimeProvider TimeProvider => context.DomainContext.TimeProvider;

        /// <summary>Gets the actor logger.</summary>
        public ILogger<MarketOutlookSnapshotCommandActor> Logger => context.DomainContext.Logger;
    }
}
