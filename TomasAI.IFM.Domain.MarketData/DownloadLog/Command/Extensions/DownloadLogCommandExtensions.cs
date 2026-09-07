using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Extensions;

/// <summary>
/// Provides DownloadLog-specific members for a typed <see cref="DownloadLogCommandActor"/> command context.
/// </summary>
public static class DownloadLogCommandExtensions
{
    extension(ICommandActorContext<DownloadLogCommandActor> context)
    {
        /// <summary>
        /// Gets the DownloadLog database-context factory exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        public IDbContextFactory DbFactory
            => GetContext(context).DbFactory;

        /// <summary>
        /// Gets the blackboard service exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        public IBlackboardService BlackboardService
            => GetContext(context).BlackboardService;

        /// <summary>
        /// Gets the logger exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// The context is <see langword="null"/>, does not implement <see cref="IDownloadLogCommandContext"/>, or exposes a
        /// <see langword="null"/> logger.
        /// </exception>
        public ILogger<DownloadLogCommandActor> Logger
            => GetContext(context).Logger;

        /// <summary>
        /// Gets the event-source database context exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        public IEventSourceActorDbContext DbEventSource
            => GetContext(context).DbEventSource;

        /// <summary>
        /// Gets the durable replay queue exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        public IDurableReplayQueue DurableReplayQueue
            => GetContext(context).DurableReplayQueue;

        /// <summary>
        /// Gets the actor-state factory exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        public IEventSourceActorStateFactory StateFactory
            => GetContext(context).StateFactory;

        /// <summary>
        /// Gets the actor service exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        public IActorService ActorService
            => GetContext(context).ActorService;

        /// <summary>
        /// Gets the DownloadLog event projector exposed by the underlying <see cref="IDownloadLogCommandContext"/>.
        /// </summary>
        public IEventProjector<DownloadLogCommandActor> EventProjector
            => GetContext(context).EventProjector;
    }

    static IDownloadLogCommandContext GetContext(ICommandActorContext<DownloadLogCommandActor> context)
        => IsArgumentNull.Set(context as IDownloadLogCommandContext, nameof(context))!;
}
