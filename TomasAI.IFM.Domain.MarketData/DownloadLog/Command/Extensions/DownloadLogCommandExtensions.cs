using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Extensions;
/// <summary>
/// Provides DownloadLog-specific members for a typed <see cref = "DownloadLogCommandActor"/> command context.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class DownloadLogCommandExtensions
{
#pragma warning disable S3398 // C# 14 extension lowering diagnostic; this source has no redundant cast or local function to move.
    static IDownloadLogCommandContext GetContext(ICommandActorContext<DownloadLogCommandActor> context) => IsArgumentNull.Named(context as IDownloadLogCommandContext, nameof(context))!;
#pragma warning restore S3398
    extension(ICommandActorContext<DownloadLogCommandActor> context)
    {
        /// <summary>
        /// Gets the DownloadLog database-context factory exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        public IDbContextFactory DbFactory => GetContext(context).DbFactory;
        /// <summary>
        /// Gets the blackboard service exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        public IBlackboardService BlackboardService => GetContext(context).BlackboardService;
        /// <summary>
        /// Gets the logger exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        /// <exception cref = "ArgumentNullException">
        /// The context is <see langword="null"/>, does not implement <see cref = "IDownloadLogCommandContext"/>, or exposes a
        /// <see langword="null"/> logger.
        /// </exception>
        public ILogger<DownloadLogCommandActor> Logger => GetContext(context).Logger;
        /// <summary>
        /// Gets the event-source database context exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        public IEventSourceActorDbContext DbEventSource => GetContext(context).DbEventSource;
        /// <summary>
        /// Gets the durable replay queue exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        public IDurableReplayQueue DurableReplayQueue => GetContext(context).DurableReplayQueue;
        /// <summary>
        /// Gets the actor-state factory exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        public IEventSourceActorStateFactory StateFactory => GetContext(context).StateFactory;
        /// <summary>
        /// Gets the actor service exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        public IActorService ActorService => GetContext(context).ActorService;
        /// <summary>
        /// Gets the DownloadLog event projector exposed by the underlying <see cref = "IDownloadLogCommandContext"/>.
        /// </summary>
        public IEventProjector<DownloadLogCommandActor> EventProjector => GetContext(context).EventProjector;
    }
}
